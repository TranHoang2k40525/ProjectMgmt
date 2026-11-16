using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Application.Services;
using DeliveryIntelligence.Domain.Entities;
using ProjectMgmt.BuildingBlocks.Results;
using ProjectMgmt.IssueTracking.Contracts;
using ProjectMgmt.Tests.Support;
using Xunit;

namespace ProjectMgmt.Tests;

/// <summary>SCRUMAI-202: Acceptance Criteria và kỹ năng yêu cầu — cầu nối dữ liệu sang hai AI.</summary>
public class IssueRequirementServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 8, 0, 0, DateTimeKind.Utc);
    private readonly InMemoryIssueRepository _issues = new();
    private readonly InMemoryRequirementRepository _repository = new();
    private readonly IssueRequirementService _service;

    public IssueRequirementServiceTests()
    {
        _service = new IssueRequirementService(_issues, _repository, new SharedSkillCatalogReader(), new SharedUserLookupService(),
            new SaveCriterionDtoValidator(), new SaveRequiredSkillDtoValidator(), new FixedTimeProvider(Now));
    }

    private Task<Result<AcceptanceCriterionDto>> Add(string content, bool met = false) =>
        _service.AddCriterionAsync(SharedTestData.StoryIssueId, new SaveCriterionDto { Content = content, IsMet = met }, SharedTestData.OwnerUserId);

    [Fact]
    public async Task CriteriaAreAppendedInOrderAsManual()
    {
        var first = (await Add(" Gửi OTP qua email ")).Value;
        var second = (await Add("OTP hết hạn sau 5 phút")).Value;

        Assert.Equal(("Gửi OTP qua email", 0, "Manual"), (first.Content, first.OrderIndex, first.Source));
        Assert.Equal(1, second.OrderIndex);
        Assert.Equal(2, _issues.Activities.Count(a => a.Action == "CriteriaAdded"));
    }

    [Fact]
    public async Task TickRecordsWhoAndWhenAndUntickClearsIt()
    {
        var criterion = (await Add("Đăng nhập thành công")).Value;

        var met = await _service.UpdateCriterionAsync(criterion.Id, new SaveCriterionDto { Content = criterion.Content, IsMet = true }, SharedTestData.DeveloperUserId);
        Assert.Equal((true, SharedTestData.DeveloperUserId, "Demo Developer", Now), (met.Value.IsMet, met.Value.MetBy!.Value, met.Value.MetByName, met.Value.MetAt!.Value));

        var unmet = await _service.UpdateCriterionAsync(criterion.Id, new SaveCriterionDto { Content = criterion.Content, IsMet = false }, SharedTestData.DeveloperUserId);
        Assert.Null(unmet.Value.MetBy);
        Assert.Null(unmet.Value.MetAt);
        Assert.Equal(["CriteriaAdded", "CriteriaMet", "CriteriaUnmet"], _issues.Activities.Select(a => a.Action));
    }

    [Fact]
    public async Task EditingAiCriterionMarksWasEditedAfterAi()
    {
        var ai = new AcceptanceCriteria { Id = Guid.NewGuid(), IssueId = SharedTestData.StoryIssueId, Content = "AI viết", Source = "AI", AiGenerationLogId = Guid.NewGuid(), CreatedAt = Now };
        _repository.Criteria.Add(ai);

        var sameText = await _service.UpdateCriterionAsync(ai.Id, new SaveCriterionDto { Content = "AI viết", IsMet = true }, SharedTestData.OwnerUserId);
        Assert.False(sameText.Value.WasEditedAfterAi);

        var edited = await _service.UpdateCriterionAsync(ai.Id, new SaveCriterionDto { Content = "Người sửa lại", IsMet = true }, SharedTestData.OwnerUserId);
        Assert.True(edited.Value.WasEditedAfterAi);
        Assert.Equal("AI", edited.Value.Source);
    }

    [Fact]
    public async Task ReorderRequiresExactCurrentSet()
    {
        var a = (await Add("A")).Value;
        var b = (await Add("B")).Value;
        var c = (await Add("C")).Value;

        var reordered = await _service.ReorderCriteriaAsync(SharedTestData.StoryIssueId, new ReorderCriteriaDto { OrderedIds = [c.Id, a.Id, b.Id] });
        Assert.Equal(["C", "A", "B"], reordered.Value.Select(x => x.Content));

        Assert.Equal("CRITERIA_REORDER_MISMATCH", (await _service.ReorderCriteriaAsync(SharedTestData.StoryIssueId,
            new ReorderCriteriaDto { OrderedIds = [a.Id, b.Id] })).Error.Code);
        Assert.Equal("CRITERIA_REORDER_MISMATCH", (await _service.ReorderCriteriaAsync(SharedTestData.StoryIssueId,
            new ReorderCriteriaDto { OrderedIds = [a.Id, a.Id, b.Id] })).Error.Code);
    }

    [Fact]
    public async Task CriterionValidationAndDelete()
    {
        Assert.Equal("CRITERIA_CONTENT_REQUIRED", (await Add("  ")).Error.Code);
        Assert.Equal("CRITERIA_CONTENT_TOO_LONG", (await Add(new string('x', 4001))).Error.Code);
        var criterion = (await Add("Xóa tôi")).Value;
        Assert.True((await _service.DeleteCriterionAsync(criterion.Id, SharedTestData.OwnerUserId)).IsSuccess);
        Assert.Empty(_repository.Criteria);
        Assert.Equal("CRITERIA_NOT_FOUND", (await _service.DeleteCriterionAsync(criterion.Id, SharedTestData.OwnerUserId)).Error.Code);
    }

    [Fact]
    public async Task RequiredSkillsValidateCatalogDuplicatesAndRanges()
    {
        var added = await _service.AddRequiredSkillAsync(SharedTestData.StoryIssueId,
            new SaveRequiredSkillDto { SkillId = SharedTestData.DotnetSkillId, MinLevel = 3, Weight = 1.5m }, SharedTestData.OwnerUserId);
        Assert.Equal((".NET / C#", 3, 1.5m, "Manual"), (added.Value.SkillName, added.Value.MinLevel, added.Value.Weight, added.Value.Source));

        Assert.Equal(ErrorType.Conflict, (await _service.AddRequiredSkillAsync(SharedTestData.StoryIssueId,
            new SaveRequiredSkillDto { SkillId = SharedTestData.DotnetSkillId }, SharedTestData.OwnerUserId)).Error.Type);
        Assert.Equal("REQUIRED_SKILL_UNKNOWN", (await _service.AddRequiredSkillAsync(SharedTestData.StoryIssueId,
            new SaveRequiredSkillDto { SkillId = SharedSkillCatalogReader.RetiredSkillId }, SharedTestData.OwnerUserId)).Error.Code);
        Assert.Equal("REQUIRED_SKILL_LEVEL_INVALID", (await _service.AddRequiredSkillAsync(SharedTestData.StoryIssueId,
            new SaveRequiredSkillDto { SkillId = SharedTestData.SqlSkillId, MinLevel = 6 }, SharedTestData.OwnerUserId)).Error.Code);
        Assert.Equal("REQUIRED_SKILL_WEIGHT_INVALID", (await _service.AddRequiredSkillAsync(SharedTestData.StoryIssueId,
            new SaveRequiredSkillDto { SkillId = SharedTestData.SqlSkillId, Weight = 0 }, SharedTestData.OwnerUserId)).Error.Code);
    }

    [Fact]
    public async Task UpdatingSkillKeepsAiSourceAndCannotSwapSkill()
    {
        var ai = new IssueRequiredSkill { Id = Guid.NewGuid(), IssueId = SharedTestData.StoryIssueId, SkillId = SharedTestData.AngularSkillId, MinLevel = 2, Weight = 1, Source = "AI" };
        _repository.Skills.Add(ai);

        var updated = await _service.UpdateRequiredSkillAsync(ai.Id, new SaveRequiredSkillDto { SkillId = SharedTestData.AngularSkillId, MinLevel = 4, Weight = 0.5m });
        Assert.Equal((4, 0.5m, "AI"), (updated.Value.MinLevel, updated.Value.Weight, updated.Value.Source));
        Assert.Equal("REQUIRED_SKILL_IMMUTABLE", (await _service.UpdateRequiredSkillAsync(ai.Id,
            new SaveRequiredSkillDto { SkillId = SharedTestData.SqlSkillId })).Error.Code);
    }

    [Fact]
    public async Task SkillContractReturnsBatchForAssignmentAi()
    {
        _repository.Skills.Add(new IssueRequiredSkill { Id = Guid.NewGuid(), IssueId = SharedTestData.StoryIssueId, SkillId = SharedTestData.DotnetSkillId, MinLevel = 3, Weight = 1, Source = "Manual" });
        _repository.Skills.Add(new IssueRequiredSkill { Id = Guid.NewGuid(), IssueId = SharedTestData.BugIssueId, SkillId = SharedTestData.AngularSkillId, MinLevel = 2, Weight = 2, Source = "AI" });
        _repository.Skills.Add(new IssueRequiredSkill { Id = Guid.NewGuid(), IssueId = SharedTestData.EpicIssueId, SkillId = SharedTestData.SqlSkillId, MinLevel = 1, Weight = 1, Source = "Manual" });

        IIssueSkillService contract = _service;
        var batch = await contract.GetRequiredSkillsAsync([SharedTestData.StoryIssueId, SharedTestData.BugIssueId]);
        Assert.Equal(2, batch.Count);
        Assert.Contains(batch, s => s.IssueId == SharedTestData.BugIssueId && s.Source == "AI" && s.MinLevel == 2);
    }
}

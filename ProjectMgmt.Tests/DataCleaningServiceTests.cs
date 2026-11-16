using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Application.Services;
using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Domain.UseCases;
using Microsoft.EntityFrameworkCore;
using ProjectMgmt.Tests.Support;
using Xunit;

namespace ProjectMgmt.Tests;

/// <summary>SCRUMAI-212 (phần BE): rule làm sạch dạng cấu hình, quality flag và QualityStatus.</summary>
public class DataCleaningServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);
    private readonly InMemoryDataOpsRepository _data = new();
    private readonly DataCleaningService _service;
    private readonly AiDatasetVersion _version;

    public DataCleaningServiceTests()
    {
        _service = new DataCleaningService(_data, new FixedTimeProvider(Now));
        var dataset = new AiDataset { Id = Guid.NewGuid(), Code = "breakdown-vi", Name = "Breakdown", TaskType = "Breakdown", CreatedAt = Now };
        _version = new AiDatasetVersion { Id = Guid.NewGuid(), DatasetId = dataset.Id, VersionTag = "v1.0", CreatedAt = Now };
        _data.Datasets.Add(dataset);
        _data.Versions.Add(_version);
        _data.Rules.AddRange(SeedRules());
    }

    /// <summary>Giống 5 rule do migration SeedDataCleaningRules tạo.</summary>
    public static List<AiDataCleaningRule> SeedRules() =>
    [
        Rule("empty-or-too-short", "LengthCheck", "Error", """{"field":"Instruction","min":15,"max":20000}""", "All"),
        Rule("generic-task", "Heuristic", "Warning", """{"arrayPath":"subTasks","field":"title","minWords":3,"phrases":["làm task","xử lý","todo"]}""", "Breakdown"),
        Rule("invalid-output-schema", "SchemaValidation", "Error", """{"requiredArray":"subTasks"}""", "Breakdown"),
        Rule("duplicate-instruction", "Deduplication", "Warning", """{"field":"Instruction"}""", "All"),
        Rule("pii-candidate", "PiiDetection", "Warning", """{"detectors":["email","phone","secret"]}""", "All")
    ];

    private static AiDataCleaningRule Rule(string code, string type, string severity, string config, string appliesTo) => new()
    {
        Id = Guid.NewGuid(), Code = code, Name = code, RuleType = type, Severity = severity, Config = config, AppliesTo = appliesTo, IsActive = true, CreatedAt = Now
    };

    private AiDatasetSample AddSample(string instruction, string output, string status = "Raw", string review = "NotReviewed")
    {
        var sample = new AiDatasetSample
        {
            Id = Guid.NewGuid(), DatasetVersionId = _version.Id, SourceType = "ManualCurated", Instruction = instruction,
            InputJson = """{"title":"x"}""", OutputJson = output, Language = "vi", SplitType = "Train",
            QualityStatus = status, ReviewStatus = review, ContentHash = Guid.NewGuid().ToString("N"), CreatedAt = Now.AddSeconds(_data.Samples.Count)
        };
        _data.Samples.Add(sample);
        return sample;
    }

    private const string GoodOutput = """{"subTasks":[{"title":"Tạo endpoint gửi OTP qua email"},{"title":"Viết test hết hạn OTP"}]}""";

    private IEnumerable<string> FlagCodes(AiDatasetSample sample) =>
        _data.Flags.Where(f => f.SampleId == sample.Id && !f.IsResolved).Select(f => _data.Rules.Single(r => r.Id == f.RuleId).Code).OrderBy(c => c);

    [Fact]
    public async Task CleanSampleBecomesCleanedWithoutFlags()
    {
        var sample = AddSample("Phân rã Story đặt lại mật khẩu cho người dùng", GoodOutput);
        var summary = await _service.RunAsync(_version.Id);

        Assert.Equal((1, 5, 0, 1, 0), (summary.Value.SamplesChecked, summary.Value.RulesApplied, summary.Value.FlagsCreated, summary.Value.Cleaned, summary.Value.Rejected));
        Assert.Equal("Cleaned", sample.QualityStatus);
    }

    [Fact]
    public async Task ErrorRulesRejectAndWarningsOnlyFlag()
    {
        var tooShort = AddSample("Ngắn", GoodOutput);
        var noArray = AddSample("Phân rã Story quản lý hồ sơ người dùng", """{"items":[]}""");
        var generic = AddSample("Phân rã Story xuất báo cáo tháng", """{"subTasks":[{"title":"todo"},{"title":"Xây API xuất báo cáo Excel"}]}""");

        var summary = await _service.RunAsync(_version.Id);

        Assert.Equal(("Rejected", "Rejected", "Cleaned"), (tooShort.QualityStatus, noArray.QualityStatus, generic.QualityStatus));
        Assert.Contains("empty-or-too-short", FlagCodes(tooShort));
        Assert.Contains("invalid-output-schema", FlagCodes(noArray));
        var genericFlag = _data.Flags.Single(f => f.SampleId == generic.Id);
        Assert.Equal(("Warning", "$.output.subTasks[0].title"), (genericFlag.Severity, genericFlag.FieldPath));
        Assert.Equal(2, summary.Value.Rejected);
    }

    [Fact]
    public async Task PiiAndDuplicateInstructionsAreFlaggedWithoutLeakingValues()
    {
        var pii = AddSample("Gửi OTP tới an.nguyen@example.com hoặc 0912 345 678", GoodOutput);
        var dup1 = AddSample("Phân rã Story thanh toán đơn hàng", GoodOutput);
        var dup2 = AddSample("  phân rã story   thanh toán đơn hàng ", """{"subTasks":[{"title":"Kết nối cổng thanh toán VNPay"}]}""");

        await _service.RunAsync(_version.Id);

        var piiMessages = _data.Flags.Where(f => f.SampleId == pii.Id).Select(f => f.Message!).ToList();
        Assert.Equal(2, piiMessages.Count);
        Assert.DoesNotContain(piiMessages, m => m.Contains("example.com", StringComparison.Ordinal) || m.Contains("345", StringComparison.Ordinal));
        Assert.Equal(["duplicate-instruction"], FlagCodes(dup1));
        Assert.Equal(["duplicate-instruction"], FlagCodes(dup2));
        Assert.Equal("Cleaned", pii.QualityStatus);
    }

    [Fact]
    public async Task RerunReplacesOpenFlagsKeepsResolvedAndRespectsReviewDecisions()
    {
        var sample = AddSample("Ngắn", GoodOutput);
        await _service.RunAsync(_version.Id);
        var first = _data.Flags.Single();
        await _service.ResolveFlagAsync(first.Id, SharedTestData.OwnerUserId);
        await _service.RunAsync(_version.Id);

        Assert.Equal(2, _data.Flags.Count);
        Assert.Single(_data.Flags, f => f.IsResolved && f.ResolvedBy == SharedTestData.OwnerUserId);

        var approved = AddSample("Phân rã Story cũ đã duyệt nhưng output sai", """{"x":1}""", "Approved", "Approved");
        var reviewerRejected = AddSample("Phân rã Story người duyệt đã loại bỏ", GoodOutput, "Rejected", "Rejected");
        await _service.RunAsync(_version.Id);
        Assert.Equal(("Rejected", "NeedsFix"), (approved.QualityStatus, approved.ReviewStatus));
        Assert.Equal("Rejected", reviewerRejected.QualityStatus);
        Assert.Equal("Rejected", sample.QualityStatus);
    }

    [Fact]
    public async Task RulesFilterByTaskTypeAndInactiveOrUnsupportedAreSkipped()
    {
        _data.Datasets.Single().TaskType = "Assignment";
        _data.Rules.Single(r => r.Code == "pii-candidate").IsActive = false;
        _data.Rules.Add(Rule("language", "LanguageCheck", "Error", "{}", "All"));
        AddSample("Xếp hạng ứng viên cho Issue tạo endpoint", """{"rank":[1,2]}""");

        var summary = await _service.RunAsync(_version.Id);
        Assert.Equal(2, summary.Value.RulesApplied);
        Assert.Equal(1, summary.Value.Cleaned);
    }

    [Fact]
    public async Task FrozenVersionCannotBeCleanedOrHaveFlagsResolved()
    {
        var sample = AddSample("Ngắn", GoodOutput);
        await _service.RunAsync(_version.Id);
        _version.IsFrozen = true;

        Assert.Equal("DATASET_VERSION_FROZEN", (await _service.RunAsync(_version.Id)).Error.Code);
        Assert.Equal("DATASET_VERSION_FROZEN", (await _service.ResolveFlagAsync(_data.Flags.Single(f => f.SampleId == sample.Id).Id, SharedTestData.OwnerUserId)).Error.Code);
    }

    [Fact]
    public async Task RuleCrudValidatesTypeSeverityAndConfig()
    {
        var created = await _service.CreateRuleAsync(new CreateCleaningRuleDto { Code = "max-output", Name = "Output quá dài", RuleType = "LengthCheck", Severity = "Warning", Config = """{ "field": "OutputJson", "max": 50000 }""" });
        Assert.Equal("""{"field":"OutputJson","max":50000}""", created.Value.Config);
        Assert.True(created.Value.IsSupported);

        Assert.Equal("CLEANING_RULE_TYPE_UNSUPPORTED", (await _service.CreateRuleAsync(new CreateCleaningRuleDto { Code = "x-y", Name = "x", RuleType = "LlmJudge" })).Error.Code);
        Assert.Equal("CLEANING_RULE_SEVERITY_INVALID", (await _service.CreateRuleAsync(new CreateCleaningRuleDto { Code = "x-y", Name = "x", RuleType = "LengthCheck", Severity = "Fatal" })).Error.Code);
        Assert.Equal("CLEANING_RULE_CONFIG_INVALID", (await _service.CreateRuleAsync(new CreateCleaningRuleDto { Code = "x-y", Name = "x", RuleType = "LengthCheck", Config = "[1]" })).Error.Code);
        Assert.Equal("CLEANING_RULE_DUPLICATE", (await _service.CreateRuleAsync(new CreateCleaningRuleDto { Code = "max-output", Name = "x", RuleType = "LengthCheck" })).Error.Code);

        var disabled = await _service.UpdateRuleAsync(created.Value.Id, new UpdateCleaningRuleDto { Name = "Output quá dài", Severity = "Error", AppliesTo = "Breakdown", IsActive = false });
        Assert.Equal(("Error", "Breakdown", false), (disabled.Value.Severity, disabled.Value.AppliesTo, disabled.Value.IsActive));
    }

    [Theory]
    [InlineData("Token: sk-abcdefghijklmnopqrstuvwxyz123", true)]
    [InlineData("password = hunter22!", true)]
    [InlineData("Gọi +84 912345678", true)]
    [InlineData("Đơn 2026-10-15 có 12 sản phẩm", false)]
    public void PiiDetectorCatchesSecretsAndPhonesButNotPlainNumbers(string text, bool flagged)
    {
        var rule = SeedRules().Single(r => r.Code == "pii-candidate");
        var sample = new AiDatasetSample { Instruction = text, InputJson = "{}", OutputJson = "{}" };
        Assert.Equal(flagged, CleaningRuleEvaluator.Evaluate(rule, sample).Count > 0);
    }
}

[Collection(MySqlCollection.Name)]
public class DataCleaningRuleSeedMySqlTests(MySqlTestDatabase database)
{
    [MySqlFact]
    public async Task MigrationSeedsFiveActiveSupportedRules()
    {
        await using var context = database.CreateDeliveryContext();
        var rules = await context.AiDataCleaningRules.AsNoTracking().Where(r => r.Id.ToString().StartsWith("55555555")).ToListAsync();
        Assert.Equal(["duplicate-instruction", "empty-or-too-short", "generic-task", "invalid-output-schema", "pii-candidate"], rules.Select(r => r.Code).OrderBy(c => c));
        Assert.All(rules, r => Assert.True(r.IsActive && CleaningRuleEvaluator.SupportedTypes.Contains(r.RuleType) && SampleHasher.IsValidJson(r.Config)));
    }
}

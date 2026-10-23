using System.Net;
using System.Net.Http.Json;
using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Application.Services;
using DeliveryIntelligence.Domain.Entities;
using ProjectMgmt.Tests.Support;
using Xunit;

namespace ProjectMgmt.Tests;

/// <summary>SCRUMAI-213: duyệt/từ chối mẫu và điều kiện đóng băng version.</summary>
public class DatasetReviewServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 16, 0, 0, 0, DateTimeKind.Utc);
    private readonly InMemoryDataOpsRepository _data = new();
    private readonly DatasetReviewService _review;
    private readonly DataCleaningService _cleaning;
    private readonly AiDatasetVersion _version;

    public DatasetReviewServiceTests()
    {
        var clock = new FixedTimeProvider(Now);
        _review = new DatasetReviewService(_data, clock);
        _cleaning = new DataCleaningService(_data, clock);
        var dataset = new AiDataset { Id = Guid.NewGuid(), Code = "breakdown-vi", Name = "Breakdown", TaskType = "Breakdown", CreatedAt = Now };
        _version = new AiDatasetVersion { Id = Guid.NewGuid(), DatasetId = dataset.Id, VersionTag = "v1.0", CreatedAt = Now };
        _data.Datasets.Add(dataset);
        _data.Versions.Add(_version);
        _data.Rules.AddRange(DataCleaningServiceTests.SeedRules());
    }

    private AiDatasetSample Add(string instruction, string split = "Train", string output = """{"subTasks":[{"title":"Tạo endpoint gửi OTP qua email"}]}""")
    {
        var sample = new AiDatasetSample
        {
            Id = Guid.NewGuid(), DatasetVersionId = _version.Id, SourceType = "ManualCurated", Instruction = instruction, InputJson = "{}",
            OutputJson = output, Language = "vi", SplitType = split, QualityStatus = "Raw", ReviewStatus = "NotReviewed",
            ContentHash = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"), CreatedAt = Now.AddSeconds(_data.Samples.Count)
        };
        _data.Samples.Add(sample);
        return sample;
    }

    private static ApproveSampleDto Approve(decimal? score = 0.9m) => new() { PiiChecked = true, QualityScore = score, Note = "OK" };

    [Fact]
    public async Task ApprovalRequiresCleanedPiiCheckAndNoBlockingFlags()
    {
        var sample = Add("Phân rã Story đặt lại mật khẩu qua email");
        Assert.Equal("SAMPLE_NOT_CLEANED", (await _review.ApproveAsync(sample.Id, Approve(), SharedTestData.SystemAdminUserId)).Error.Code);

        await _cleaning.RunAsync(_version.Id);
        Assert.Equal("SAMPLE_PII_NOT_CHECKED", (await _review.ApproveAsync(sample.Id, new ApproveSampleDto(), SharedTestData.SystemAdminUserId)).Error.Code);
        Assert.Equal("SAMPLE_QUALITY_SCORE_INVALID", (await _review.ApproveAsync(sample.Id, Approve(1.5m), SharedTestData.SystemAdminUserId)).Error.Code);

        var approved = await _review.ApproveAsync(sample.Id, Approve(0.8765m), SharedTestData.SystemAdminUserId);
        Assert.Equal(("Approved", "Approved", true, 0.877m), (approved.Value.QualityStatus, approved.Value.ReviewStatus, approved.Value.IsPiiRedacted, approved.Value.QualityScore!.Value));
        Assert.Equal(SharedTestData.SystemAdminUserId, approved.Value.ReviewedBy);
    }

    [Fact]
    public async Task OpenPiiFlagBlocksApprovalUntilResolved()
    {
        var sample = Add("Gửi OTP tới khachhang@example.com khi đặt lại mật khẩu");
        await _cleaning.RunAsync(_version.Id);
        Assert.Equal("SAMPLE_OPEN_FLAGS", (await _review.ApproveAsync(sample.Id, Approve(), SharedTestData.SystemAdminUserId)).Error.Code);

        foreach (var flag in _data.Flags.Where(f => f.SampleId == sample.Id).ToList())
        {
            await _cleaning.ResolveFlagAsync(flag.Id, SharedTestData.SystemAdminUserId);
        }

        Assert.True((await _review.ApproveAsync(sample.Id, Approve(), SharedTestData.SystemAdminUserId)).IsSuccess);
    }

    [Fact]
    public async Task RejectNeedsReason()
    {
        var sample = Add("Phân rã Story không rõ ràng cần bỏ");
        Assert.Equal("SAMPLE_REJECT_NOTE_REQUIRED", (await _review.RejectAsync(sample.Id, new RejectSampleDto(), SharedTestData.SystemAdminUserId)).Error.Code);
        var rejected = await _review.RejectAsync(sample.Id, new RejectSampleDto { Note = "Output sai nghiệp vụ" }, SharedTestData.SystemAdminUserId);
        Assert.Equal(("Rejected", "Rejected", "Output sai nghiệp vụ"), (rejected.Value.QualityStatus, rejected.Value.ReviewStatus, rejected.Value.ReviewNote));
    }

    [Fact]
    public async Task FreezeRequiresDecisionsAndLocksCountsSeedAndChecksum()
    {
        var train = Add("Phân rã Story đăng nhập bằng Google", "Train");
        var test = Add("Phân rã Story xuất báo cáo Excel", "Test");
        var bad = Add("Ngắn");
        await _cleaning.RunAsync(_version.Id);
        await _review.ApproveAsync(train.Id, Approve(), SharedTestData.SystemAdminUserId);

        Assert.Equal("FREEZE_PENDING_SAMPLES", (await _review.FreezeAsync(_version.Id, new FreezeVersionDto())).Error.Code);
        await _review.ApproveAsync(test.Id, Approve(), SharedTestData.SystemAdminUserId);
        Assert.Equal("Rejected", bad.QualityStatus);

        var frozen = await _review.FreezeAsync(_version.Id, new FreezeVersionDto { SplitSeed = 42 });
        Assert.True(frozen.Value.IsFrozen);
        Assert.Equal((2, 1, 0, 1, 42), (frozen.Value.SampleCount, frozen.Value.TrainCount, frozen.Value.ValidationCount, frozen.Value.TestCount, frozen.Value.SplitSeed!.Value));
        Assert.Equal(DatasetReviewService.ManifestChecksum([test, train]), frozen.Value.Checksum);
        Assert.Equal(Now, frozen.Value.FrozenAt);

        Assert.Equal("DATASET_VERSION_ALREADY_FROZEN", (await _review.FreezeAsync(_version.Id, new FreezeVersionDto())).Error.Code);
        Assert.Equal("DATASET_VERSION_FROZEN", (await _review.RejectAsync(train.Id, new RejectSampleDto { Note = "x" }, SharedTestData.SystemAdminUserId)).Error.Code);
    }

    [Fact]
    public async Task FreezeNeedsAtLeastOneApprovedSample()
    {
        var only = Add("Phân rã Story sẽ bị từ chối hết");
        await _review.RejectAsync(only.Id, new RejectSampleDto { Note = "Không dùng" }, SharedTestData.SystemAdminUserId);
        Assert.Equal("FREEZE_NO_APPROVED_SAMPLES", (await _review.FreezeAsync(_version.Id, new FreezeVersionDto())).Error.Code);
    }

    [Fact]
    public async Task ResolvedFlagStaysResolvedWhenRulesRunAgain()
    {
        var sample = Add("Gửi OTP tới khachhang@example.com khi đặt lại mật khẩu");
        await _cleaning.RunAsync(_version.Id);
        var pii = _data.Flags.Single(f => f.SampleId == sample.Id);
        await _cleaning.ResolveFlagAsync(pii.Id, SharedTestData.SystemAdminUserId);
        await _review.ApproveAsync(sample.Id, Approve(), SharedTestData.SystemAdminUserId);

        var rerun = await _cleaning.RunAsync(_version.Id);

        Assert.Equal(0, rerun.Value.FlagsCreated);
        Assert.Single(_data.Flags, f => f.SampleId == sample.Id);
        Assert.Equal("Approved", sample.QualityStatus);
        Assert.True((await _review.FreezeAsync(_version.Id, new FreezeVersionDto())).IsSuccess);
    }

    [Fact]
    public async Task EditingSampleDropsFlagsOfOldContent()
    {
        var datasets = new DatasetService(_data, new FixedTimeProvider(Now));
        var sample = Add("Gửi OTP tới khachhang@example.com khi đặt lại mật khẩu");
        await _cleaning.RunAsync(_version.Id);
        await _cleaning.ResolveFlagAsync(_data.Flags.Single(f => f.SampleId == sample.Id).Id, SharedTestData.SystemAdminUserId);

        await datasets.UpdateSampleAsync(sample.Id, new SaveDatasetSampleDto
        {
            Instruction = "Gửi OTP tới nguoidung@example.com khi đặt lại mật khẩu",
            OutputJson = """{"subTasks":[{"title":"Tạo endpoint gửi OTP qua email"}]}"""
        });
        Assert.DoesNotContain(_data.Flags, f => f.SampleId == sample.Id);

        await _cleaning.RunAsync(_version.Id);
        Assert.Contains(_data.Flags, f => f.SampleId == sample.Id && !f.IsResolved);
        Assert.Equal("SAMPLE_OPEN_FLAGS", (await _review.ApproveAsync(sample.Id, Approve(), SharedTestData.SystemAdminUserId)).Error.Code);
    }

    [Fact]
    public async Task WrongTypedRuleConfigIsIgnoredInsteadOfFailingTheRun()
    {
        _data.Rules.Clear();
        _data.Rules.Add(new AiDataCleaningRule
        {
            Id = Guid.NewGuid(), Code = "bad-config", Name = "Bad", RuleType = "LengthCheck", Severity = "Error",
            Config = """{"field":"Instruction","min":"15"}""", AppliesTo = "All", IsActive = true, CreatedAt = Now
        });
        _data.Rules.Add(new AiDataCleaningRule
        {
            Id = Guid.NewGuid(), Code = "bad-pii", Name = "Bad PII", RuleType = "PiiDetection", Severity = "Warning",
            Config = """{"detectors":[1,"email"]}""", AppliesTo = "All", IsActive = true, CreatedAt = Now
        });
        Add("Gửi OTP tới khachhang@example.com");

        var run = await _cleaning.RunAsync(_version.Id);

        Assert.True(run.IsSuccess);
        Assert.Equal(1, run.Value.FlagsCreated);
    }

    [Fact]
    public async Task ApprovalNoteOverLimitIsRejectedNotTruncated()
    {
        var sample = Add("Phân rã Story đặt lại mật khẩu qua email");
        await _cleaning.RunAsync(_version.Id);
        var result = await _review.ApproveAsync(sample.Id, new ApproveSampleDto { PiiChecked = true, Note = new string('x', 1001) }, SharedTestData.SystemAdminUserId);
        Assert.Equal("SAMPLE_REVIEW_NOTE_TOO_LONG", result.Error.Code);
    }

    [Fact]
    public void ManifestChecksumIsOrderIndependentAndSplitSensitive()
    {
        var a = new AiDatasetSample { ContentHash = "aa", SplitType = "Train" };
        var b = new AiDatasetSample { ContentHash = "bb", SplitType = "Test" };
        Assert.Equal(DatasetReviewService.ManifestChecksum([a, b]), DatasetReviewService.ManifestChecksum([b, a]));
        Assert.NotEqual(DatasetReviewService.ManifestChecksum([a, b]),
            DatasetReviewService.ManifestChecksum([a, new AiDatasetSample { ContentHash = "bb", SplitType = "Train" }]));
    }
}

/// <summary>Luồng DataOps đầy đủ qua Host thật + MySQL: tạo → làm sạch → duyệt → freeze, và chặn quyền.</summary>
public class DataOpsApiMySqlTests(DeliveryApiFixture fixture) : IClassFixture<DeliveryApiFixture>
{
    private HttpClient As(Guid userId) => fixture.Factory!.CreateClientFor(userId);

    [MySqlFact]
    public async Task ProjectManagerWithoutDatasetPermissionIsForbidden()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await As(SharedTestData.OwnerUserId).GetAsync("/api/v1/ai-data/datasets")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await As(SharedTestData.OwnerUserId).GetAsync("/api/v1/ai-data/cleaning-rules")).StatusCode);
    }

    [MySqlFact]
    public async Task AdminRunsFullLifecycleUntilFrozen()
    {
        var admin = As(SharedTestData.SystemAdminUserId);
        var dataset = await (await admin.PostAsJsonAsync("/api/v1/ai-data/datasets",
            new CreateDatasetDto { Code = "breakdown-vi-it", Name = "Breakdown IT", TaskType = "Breakdown" })).Content.ReadFromJsonAsync<DatasetDto>();
        var version = await (await admin.PostAsJsonAsync($"/api/v1/ai-data/datasets/{dataset!.Id}/versions",
            new CreateDatasetVersionDto { VersionTag = "v1.0.0" })).Content.ReadFromJsonAsync<DatasetVersionDto>();

        var good = await (await admin.PostAsJsonAsync($"/api/v1/ai-data/versions/{version!.Id}/samples", new SaveDatasetSampleDto
        {
            Instruction = "Phân rã Story người dùng đổi mật khẩu",
            InputJson = """{"title":"Đổi mật khẩu"}""",
            OutputJson = """{"subTasks":[{"title":"Tạo API đổi mật khẩu có kiểm tra mật khẩu cũ"}]}"""
        })).Content.ReadFromJsonAsync<DatasetSampleDto>();
        var bad = await (await admin.PostAsJsonAsync($"/api/v1/ai-data/versions/{version.Id}/samples", new SaveDatasetSampleDto
        {
            Instruction = "Ngắn", OutputJson = """{"x":1}"""
        })).Content.ReadFromJsonAsync<DatasetSampleDto>();

        var summary = await (await admin.PostAsync($"/api/v1/ai-data/versions/{version.Id}/clean", null)).Content.ReadFromJsonAsync<CleaningRunSummaryDto>();
        Assert.Equal((2, 1, 1), (summary!.SamplesChecked, summary.Cleaned, summary.Rejected));
        var flags = await admin.GetFromJsonAsync<List<QualityFlagDto>>($"/api/v1/ai-data/versions/{version.Id}/flags");
        Assert.Contains(flags!, f => f.SampleId == bad!.Id && f.Severity == "Error");

        var approve = await admin.PostAsJsonAsync($"/api/v1/ai-data/samples/{good!.Id}/approve", new ApproveSampleDto { PiiChecked = true, QualityScore = 0.9m });
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        var pending = await admin.GetFromJsonAsync<PagedResultDto<DatasetSampleDto>>($"/api/v1/ai-data/versions/{version.Id}/samples?qualityStatus=Approved");
        Assert.Equal(1, pending!.TotalCount);

        // Không gửi body: seed ngẫu nhiên.
        var frozen = await (await admin.PostAsync($"/api/v1/ai-data/versions/{version.Id}/freeze", null))
            .Content.ReadFromJsonAsync<DatasetVersionDto>();
        Assert.True(frozen!.IsFrozen);
        Assert.NotNull(frozen.SplitSeed);
        Assert.Equal(64, frozen.Checksum!.Length);

        var afterFreeze = await admin.PostAsJsonAsync($"/api/v1/ai-data/versions/{version.Id}/samples", new SaveDatasetSampleDto { Instruction = "Mẫu sau khi freeze không được thêm" });
        Assert.Equal(HttpStatusCode.Conflict, afterFreeze.StatusCode);
        Assert.Contains("DATASET_VERSION_FROZEN", await afterFreeze.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }
}

using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Application.Services;
using DeliveryIntelligence.Domain.UseCases;
using DeliveryIntelligence.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using ProjectMgmt.BuildingBlocks.Results;
using ProjectMgmt.Tests.Support;
using Xunit;

namespace ProjectMgmt.Tests;

/// <summary>SCRUMAI-211: Dataset → Version → Sample, khử trùng bằng ContentHash, version đã freeze là bất biến.</summary>
public class DatasetServiceTests
{
    private readonly InMemoryDataOpsRepository _data = new();
    private readonly DatasetService _service;

    public DatasetServiceTests()
    {
        _service = new DatasetService(_data, new FixedTimeProvider(new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc)));
    }

    private async Task<(DatasetDto Dataset, DatasetVersionDto Version)> SeedAsync()
    {
        var dataset = (await _service.CreateDatasetAsync(new CreateDatasetDto { Code = "Breakdown-VI-V1", Name = "Phân rã tiếng Việt", TaskType = "Breakdown" }, SharedTestData.OwnerUserId)).Value;
        var version = (await _service.CreateVersionAsync(dataset.Id, new CreateDatasetVersionDto { VersionTag = "v1.0.0" })).Value;
        return (dataset, version);
    }

    private static SaveDatasetSampleDto Sample(string instruction = "Phân rã Story") => new()
    {
        Instruction = instruction,
        InputJson = """{ "title": "Đặt lại mật khẩu" }""",
        OutputJson = """{"subTasks":[{"title":"Tạo endpoint"}]}"""
    };

    [Fact]
    public async Task DatasetAndVersionValidation()
    {
        var (dataset, version) = await SeedAsync();
        Assert.Equal("breakdown-vi-v1", dataset.Code);
        Assert.Equal(["vi"], dataset.Languages);
        Assert.Equal("v1.0.0", version.VersionTag);

        Assert.Equal(ErrorType.Conflict, (await _service.CreateDatasetAsync(new CreateDatasetDto { Code = "breakdown-vi-v1", Name = "Trùng", TaskType = "Breakdown" }, SharedTestData.OwnerUserId)).Error.Type);
        Assert.Equal("DATASET_TASK_TYPE_INVALID", (await _service.CreateDatasetAsync(new CreateDatasetDto { Code = "x-1", Name = "Sai loại", TaskType = "Chat" }, SharedTestData.OwnerUserId)).Error.Code);
        Assert.Equal("DATASET_CODE_INVALID", (await _service.CreateDatasetAsync(new CreateDatasetDto { Code = "có dấu", Name = "Sai mã", TaskType = "Breakdown" }, SharedTestData.OwnerUserId)).Error.Code);
        Assert.Equal("DATASET_VERSION_TAG_INVALID", (await _service.CreateVersionAsync(dataset.Id, new CreateDatasetVersionDto { VersionTag = "latest" })).Error.Code);
        Assert.Equal(ErrorType.Conflict, (await _service.CreateVersionAsync(dataset.Id, new CreateDatasetVersionDto { VersionTag = "v1.0.0" })).Error.Type);
    }

    [Fact]
    public async Task SamplesStartRawAreCanonicalizedAndDeduplicated()
    {
        var (_, version) = await SeedAsync();
        var first = await _service.AddSampleAsync(version.Id, Sample());

        Assert.Equal(("Raw", "NotReviewed", false), (first.Value.QualityStatus, first.Value.ReviewStatus, first.Value.IsPiiRedacted));
        Assert.Equal("""{"title":"Đặt lại mật khẩu"}""", first.Value.InputJson);
        Assert.Equal(64, first.Value.ContentHash.Length);
        Assert.Equal(1, _data.Versions.Single().SampleCount);

        // Khác khoảng trắng/xuống dòng nhưng cùng nội dung ⇒ trùng.
        var duplicate = Sample("  Phân rã Story\r\n");
        duplicate.InputJson = """{"title":"Đặt lại mật khẩu"}""";
        Assert.Equal("DATASET_SAMPLE_DUPLICATE", (await _service.AddSampleAsync(version.Id, duplicate)).Error.Code);
    }

    [Fact]
    public async Task InvalidSampleFieldsAreRejected()
    {
        var (_, version) = await SeedAsync();
        var badJson = Sample();
        badJson.OutputJson = "{ not json";
        Assert.Equal("SAMPLE_JSON_INVALID", (await _service.AddSampleAsync(version.Id, badJson)).Error.Code);
        var badSource = Sample();
        badSource.SourceType = "Crawler";
        Assert.Equal("SAMPLE_SOURCE_TYPE_INVALID", (await _service.AddSampleAsync(version.Id, badSource)).Error.Code);
        var badSplit = Sample();
        badSplit.SplitType = "Dev";
        Assert.Equal("SAMPLE_SPLIT_INVALID", (await _service.AddSampleAsync(version.Id, badSplit)).Error.Code);
        Assert.Equal("SAMPLE_INSTRUCTION_INVALID", (await _service.AddSampleAsync(version.Id, Sample(" "))).Error.Code);
    }

    [Fact]
    public async Task EditingSampleResetsReviewAndRehashes()
    {
        var (_, version) = await SeedAsync();
        var sample = (await _service.AddSampleAsync(version.Id, Sample())).Value;
        var entity = _data.Samples.Single();
        entity.QualityStatus = DatasetRules.Quality.Approved;
        entity.ReviewStatus = DatasetRules.Review.Approved;
        entity.IsPiiRedacted = true;

        var updated = await _service.UpdateSampleAsync(sample.Id, Sample("Phân rã Story (đã sửa)"));
        Assert.NotEqual(sample.ContentHash, updated.Value.ContentHash);
        Assert.Equal(("Raw", "NotReviewed", false), (updated.Value.QualityStatus, updated.Value.ReviewStatus, updated.Value.IsPiiRedacted));
    }

    [Fact]
    public async Task FrozenVersionRejectsAllSampleWrites()
    {
        var (_, version) = await SeedAsync();
        var sample = (await _service.AddSampleAsync(version.Id, Sample())).Value;
        _data.Versions.Single().IsFrozen = true;

        Assert.Equal("DATASET_VERSION_FROZEN", (await _service.AddSampleAsync(version.Id, Sample("Mới"))).Error.Code);
        Assert.Equal("DATASET_VERSION_FROZEN", (await _service.UpdateSampleAsync(sample.Id, Sample("Sửa"))).Error.Code);
    }

    [Fact]
    public async Task SampleListIsFilteredAndPaged()
    {
        var (_, version) = await SeedAsync();
        for (var i = 0; i < 5; i++)
        {
            var request = Sample($"Mẫu {i}");
            request.SplitType = i % 2 == 0 ? "Train" : "Test";
            await _service.AddSampleAsync(version.Id, request);
        }

        var page = await _service.ListSamplesAsync(version.Id, new SampleQueryDto { SplitType = "Train", PageSize = 2 });
        Assert.Equal(3, page.Value.TotalCount);
        Assert.Equal(2, page.Value.Items.Count);
        Assert.Equal("SAMPLE_PAGE_INVALID", (await _service.ListSamplesAsync(version.Id, new SampleQueryDto { PageSize = 500 })).Error.Code);
    }

    [Fact]
    public void HashIgnoresFormattingButNotContent()
    {
        var a = SampleHasher.Compute("Viết test", """{"a":1, "b":[1,2]}""", "{}");
        var b = SampleHasher.Compute(" Viết test\r\n", """{ "a": 1, "b": [ 1, 2 ] }""", " { } ");
        var c = SampleHasher.Compute("Viết test", """{"a":2,"b":[1,2]}""", "{}");
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }
}

/// <summary>Ràng buộc DB của DataOps trên MySQL thật.</summary>
[Collection(MySqlCollection.Name)]
public class DataOpsRepositoryMySqlTests(MySqlTestDatabase database)
{
    [MySqlFact]
    public async Task DuplicateHashInSameVersionIsRejectedByUniqueIndexAndCountsStay()
    {
        await using var context = database.CreateDeliveryContext();
        var service = new DatasetService(new DataOpsRepository(context), TimeProvider.System);
        var dataset = (await service.CreateDatasetAsync(new CreateDatasetDto { Code = $"it-{Guid.NewGuid():N}"[..20], Name = "Integration", TaskType = "Assignment" }, SharedTestData.OwnerUserId)).Value;
        var version = (await service.CreateVersionAsync(dataset.Id, new CreateDatasetVersionDto { VersionTag = "v0.1" })).Value;
        var request = new SaveDatasetSampleDto { Instruction = "Xếp hạng ứng viên", InputJson = """{"issue":1}""", OutputJson = """{"rank":[1]}""" };

        Assert.True((await service.AddSampleAsync(version.Id, request)).IsSuccess);
        Assert.Equal("DATASET_SAMPLE_DUPLICATE", (await service.AddSampleAsync(version.Id, request)).Error.Code);

        // Bỏ qua kiểm tra ở service: unique index vẫn chặn.
        var hash = SampleHasher.Compute(request.Instruction, request.InputJson, request.OutputJson);
        context.AiDatasetSamples.Add(new DeliveryIntelligence.Domain.Entities.AiDatasetSample
        {
            Id = Guid.NewGuid(), DatasetVersionId = version.Id, SourceType = "ManualCurated", Instruction = "x", InputJson = "{}", OutputJson = "{}",
            Language = "vi", SplitType = "Train", QualityStatus = "Raw", ReviewStatus = "NotReviewed", ContentHash = hash, CreatedAt = DateTime.UtcNow
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}

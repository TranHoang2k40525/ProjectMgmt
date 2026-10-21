using System.Net;
using System.Net.Http.Json;
using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Application.Services;
using ProjectMgmt.Tests.Support;
using Xunit;

namespace ProjectMgmt.Tests;

/// <summary>SCRUMAI-203: Issue ↔ nhiều Component, Fix/Affects Version tách riêng, kiểm cùng Project.</summary>
public class IssueReleaseServiceTests
{
    private readonly InMemoryIssueRepository _issues = new();
    private readonly InMemoryReleaseLinkRepository _links = new();
    private readonly IssueReleaseService _service;

    public IssueReleaseServiceTests()
    {
        _service = new IssueReleaseService(_issues, _links, new SharedReleaseLookupService(),
            new FixedTimeProvider(new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public async Task ComponentsAreMultipleIdempotentAndNamed()
    {
        await _service.AddComponentAsync(SharedTestData.StoryIssueId, SharedReleaseLookupService.BackendComponentId, SharedTestData.OwnerUserId);
        await _service.AddComponentAsync(SharedTestData.StoryIssueId, SharedReleaseLookupService.FrontendComponentId, SharedTestData.OwnerUserId);
        var again = await _service.AddComponentAsync(SharedTestData.StoryIssueId, SharedReleaseLookupService.BackendComponentId, SharedTestData.OwnerUserId);

        Assert.Equal(["Backend API", "Web UI"], again.Value.Components.Select(c => c.Name));
        Assert.Equal(2, again.Value.AvailableComponents.Count);
        Assert.Equal(2, _issues.Activities.Count(a => a.Action == "ComponentAdded"));

        var removed = await _service.RemoveComponentAsync(SharedTestData.StoryIssueId, SharedReleaseLookupService.BackendComponentId, SharedTestData.OwnerUserId);
        Assert.Equal(["Web UI"], removed.Value.Components.Select(c => c.Name));
    }

    [Fact]
    public async Task FixAndAffectsVersionsAreSeparate()
    {
        await _service.AddVersionAsync(SharedTestData.BugIssueId, new AddIssueVersionDto { VersionId = SharedReleaseLookupService.V2VersionId, LinkType = "affectsversion" }, SharedTestData.OwnerUserId);
        var result = await _service.AddVersionAsync(SharedTestData.BugIssueId, new AddIssueVersionDto { VersionId = SharedReleaseLookupService.V1VersionId, LinkType = "FixVersion" }, SharedTestData.OwnerUserId);

        Assert.Equal("v1.0", Assert.Single(result.Value.FixVersions).Name);
        var affects = Assert.Single(result.Value.AffectsVersions);
        Assert.Equal(("v0.9", true), (affects.Name, affects.IsReleased));

        var same = await _service.AddVersionAsync(SharedTestData.BugIssueId, new AddIssueVersionDto { VersionId = SharedReleaseLookupService.V2VersionId, LinkType = "FixVersion" }, SharedTestData.OwnerUserId);
        Assert.Equal(2, same.Value.FixVersions.Count);

        var removed = await _service.RemoveVersionAsync(SharedTestData.BugIssueId, SharedReleaseLookupService.V2VersionId, "AffectsVersion", SharedTestData.OwnerUserId);
        Assert.Empty(removed.Value.AffectsVersions);
        Assert.Equal(2, removed.Value.FixVersions.Count);
    }

    [Fact]
    public async Task ForeignComponentVersionAndBadTypeAreRejected()
    {
        Assert.Equal("ISSUE_COMPONENT_INVALID", (await _service.AddComponentAsync(SharedTestData.StoryIssueId,
            SharedReleaseLookupService.OtherComponentId, SharedTestData.OwnerUserId)).Error.Code);
        Assert.Equal("ISSUE_VERSION_INVALID", (await _service.AddVersionAsync(SharedTestData.OtherProjectIssueId,
            new AddIssueVersionDto { VersionId = SharedReleaseLookupService.V1VersionId }, SharedTestData.OwnerUserId)).Error.Code);
        Assert.Equal("ISSUE_VERSION_LINK_TYPE_INVALID", (await _service.AddVersionAsync(SharedTestData.StoryIssueId,
            new AddIssueVersionDto { VersionId = SharedReleaseLookupService.V1VersionId, LinkType = "Target" }, SharedTestData.OwnerUserId)).Error.Code);
        Assert.Empty(_links.Components);
        Assert.Empty(_links.Versions);
    }
}

/// <summary>Luồng đầy đủ qua Host thật khi đã có (fake) contract Component/Version của Planning.</summary>
public class IssueReleaseApiMySqlTests(TransitionApiFixture fixture) : IClassFixture<TransitionApiFixture>
{
    [MySqlFact]
    public async Task LinksPersistAndRespectPermissions()
    {
        var url = $"/api/v1/issues/{SharedTestData.StoryIssueId}";
        var viewer = fixture.Factory!.CreateClientFor(SharedTestData.ViewerUserId);
        var developer = fixture.Factory!.CreateClientFor(SharedTestData.DeveloperUserId);

        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync($"{url}/components",
            new AddIssueComponentDto { ComponentId = SharedReleaseLookupService.BackendComponentId })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await developer.PostAsJsonAsync($"{url}/components",
            new AddIssueComponentDto { ComponentId = SharedReleaseLookupService.BackendComponentId })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await developer.PostAsJsonAsync($"{url}/versions",
            new AddIssueVersionDto { VersionId = SharedReleaseLookupService.V1VersionId, LinkType = "FixVersion" })).StatusCode);

        var links = await viewer.GetFromJsonAsync<IssueReleaseLinksDto>($"{url}/release-links");
        Assert.Equal("Backend API", Assert.Single(links!.Components).Name);
        Assert.Equal("v1.0", Assert.Single(links.FixVersions).Name);

        var removed = await developer.DeleteAsync($"{url}/versions/{SharedReleaseLookupService.V1VersionId}?linkType=FixVersion");
        Assert.Empty((await removed.Content.ReadFromJsonAsync<IssueReleaseLinksDto>())!.FixVersions);
    }
}

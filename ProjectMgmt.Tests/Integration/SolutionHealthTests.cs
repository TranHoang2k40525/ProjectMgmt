using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ProjectMgmt.Tests.Integration;

public sealed class SolutionHealthTests : IClassFixture<ProjectMgmtWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SolutionHealthTests(ProjectMgmtWebApplicationFactory factory) =>
        _client = factory.CreateClient();

    [Fact]
    public async Task LiveHealthEndpointIsAvailableWithoutDatabaseCredentials()
    {
        using var response = await _client.GetAsync("/health/live");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ReadinessReportsUnavailableWithoutDatabaseCredentials()
    {
        using var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task SystemInfoIdentifiesSolutionWebApi()
    {
        using var response = await _client.GetAsync("/api/system/info");
        var body = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("ScrumAI Project Management", body, StringComparison.Ordinal);
        Assert.Contains("modular-monolith", body, StringComparison.Ordinal);
    }
}

public sealed class ProjectMgmtWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:ProjectMgmt"] = string.Empty
            }));
    }
}

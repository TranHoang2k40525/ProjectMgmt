using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ProjectMgmt.Tests;

public class HostSecurityTests : IClassFixture<ProjectMgmtWebFactory>
{
    private readonly HttpClient _client;

    public HostSecurityTests(ProjectMgmtWebFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    public async Task HealthEndpointsArePublicForDeploymentProbes(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/notifications")]
    [InlineData("/api/v1/ai/breakdown/00000000-0000-0000-0000-000000000001")]
    [InlineData("/api/v1/ai/prompt-templates")]
    [InlineData("/hubs/notifications")]
    public async Task ModuleOneOperationalEndpointsRejectAnonymousAccess(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

public class ProjectMgmtWebFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
        });
    }
}

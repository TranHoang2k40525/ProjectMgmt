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
    private readonly Dictionary<string, string?> _originalEnvironment = new();

    public ProjectMgmtWebFactory()
    {
        SetTestEnvironment("ASPNETCORE_ENVIRONMENT", "Testing");
        SetTestEnvironment(
            "ConnectionStrings__ProjectMgmt",
            "Server=127.0.0.1;Port=3306;Database=ProjectMgmtTests;User Id=test;Password=test;");
        SetTestEnvironment("Database__ServerVersion", "8.0.46");
        SetTestEnvironment("Jwt__Issuer", "ProjectMgmt.Tests");
        SetTestEnvironment("Jwt__Audience", "ProjectMgmt.Tests.Client");
        SetTestEnvironment(
            "Jwt__SigningKey",
            "unit-test-signing-key-with-more-than-32-bytes");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        foreach (var setting in _originalEnvironment)
        {
            Environment.SetEnvironmentVariable(setting.Key, setting.Value);
        }
    }

    private void SetTestEnvironment(string key, string value)
    {
        _originalEnvironment[key] = Environment.GetEnvironmentVariable(key);
        Environment.SetEnvironmentVariable(key, value);
    }
}

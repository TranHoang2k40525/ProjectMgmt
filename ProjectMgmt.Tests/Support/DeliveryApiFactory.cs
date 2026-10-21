using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ProjectMgmt.Tests.Support;

/// <summary>
/// Host thật trỏ vào database tạm của <see cref="MySqlTestDatabase"/>. Xác thực JWT được thay bằng scheme test:
/// header "X-Test-User" mang UserId, không có header thì request là ẩn danh (401). Phân quyền vẫn đi qua RBAC thật.
/// </summary>
public class DeliveryApiFactory(MySqlTestDatabase database) : WebApplicationFactory<Program>
{
    public const string UserHeader = "X-Test-User";

    /// <summary>Đăng ký fake IWorkflowValidationService (thay cho bản thật B chưa bàn giao) để test endpoint transition.</summary>
    public bool RegisterWorkflowFake { get; init; }

    public HttpClient CreateClientFor(Guid? userId)
    {
        var client = CreateClient();
        if (userId.HasValue)
        {
            client.DefaultRequestHeaders.Add(UserHeader, userId.Value.ToString());
        }

        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:ProjectMgmt", database.ConnectionString);
        builder.ConfigureTestServices(services =>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            if (RegisterWorkflowFake)
            {
                services.AddSingleton<ProjectMgmt.ProjectManagement.Contracts.IWorkflowValidationService, SharedWorkflowValidationService>();
            }

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }
}

public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(DeliveryApiFactory.UserHeader, out var value) || !Guid.TryParse(value, out var userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

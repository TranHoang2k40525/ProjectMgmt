using IdentityExperience.Application.Dto;

namespace IdentityExperience.Application.IServices;

public interface IAccountServices
{
    Task<RegisterResult> RegisterAsync(AccountDto account, CancellationToken cancellationToken = default);
    Task<ResultLogin> LoginAsync(AccountDto account, CancellationToken cancellationToken = default);
    Task<OtpResult> SendOtpAsync(AccountDto account, CancellationToken cancellationToken = default);
    Task<OtpResult> VerifyOtpAsync(AccountDto account, CancellationToken cancellationToken = default);
    Task<ResultLogin> RefreshTokenAsync(
        string refreshToken,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);
    Task<Result> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
}

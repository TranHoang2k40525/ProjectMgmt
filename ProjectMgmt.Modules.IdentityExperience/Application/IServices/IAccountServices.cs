using IdentityExperience.Application.Dto;

namespace IdentityExperience.Application.IServices;

public interface IAccountServices
{
    Task<RegisterResult> RegisterAsync(
        string? email,
        string? password,
        string? fullName,
        string? phoneNumber);

    Task<ResultLogin> LoginAsync(
        string? email,
        string? password,
        string? ipAddress,
        string? userAgent);

    Task<OtpResult> SendOtpAsync(string? email, string? purpose);

    Task<OtpResult> VerifyOtpAsync(
        string? email,
        string? code,
        string? purpose);

    Task<ResultLogin> RefreshTokenAsync(
        string? refreshToken,
        string? ipAddress,
        string? userAgent);

    Task<Result> LogoutAsync(string? refreshToken);
}

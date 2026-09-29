namespace IdentityExperience.Application.IServices;

public interface IEmailService
{
    Task<bool> SendOtpAsync(
        string recipientEmail,
        string recipientName,
        string otpCode,
        DateTime expiresAtUtc);

    Task<bool> SendPasswordResetOtpAsync(
        string recipientEmail,
        string recipientName,
        string otpCode,
        DateTime expiresAtUtc);

    Task<bool> SendPasswordChangedAsync(
        string recipientEmail,
        string recipientName,
        DateTime changedAtUtc);
}

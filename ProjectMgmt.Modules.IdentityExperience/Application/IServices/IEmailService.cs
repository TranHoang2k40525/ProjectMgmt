namespace IdentityExperience.Application.IServices;

public interface IEmailService
{
    Task<bool> SendOtpAsync(
        string recipientEmail,
        string recipientName,
        string otpCode,
        DateTime expiresAtUtc);
}

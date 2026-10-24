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

    Task<bool> SendProjectInvitationAsync(
        string recipientEmail,
        string recipientName,
        Guid projectId,
        string? roleName);

    Task<bool> SendProjectRoleChangedAsync(
        string recipientEmail,
        string recipientName,
        Guid projectId,
        string? roleName);

    Task<bool> SendProjectRoleRevokedAsync(
        string recipientEmail,
        string recipientName,
        Guid projectId);

    Task<bool> SendInvitationAcceptedAsync(
        string recipientEmail,
        string recipientName,
        string memberName,
        string memberEmail,
        Guid projectId);

    Task<bool> SendInvitationDeclinedAsync(
        string recipientEmail,
        string recipientName,
        string memberName,
        string memberEmail,
        Guid projectId);
}

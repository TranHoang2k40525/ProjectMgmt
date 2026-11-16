using System.Net;
using System.Net.Mail;
using System.Text;
using IdentityExperience.Application.IServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IdentityExperience.Infrastructure.Services;

/// <summary>
/// SMTP adapter được chuyển từ cấu trúc cấu hình Email:* của MovieTicket.
/// Thông tin xác thực phải đến từ User Secrets hoặc biến môi trường.
/// </summary>
public class EmailService : IEmailService
{
    private static readonly Action<ILogger, string, Exception?> LogSecurityEmailDeliveryFailure =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(1001, "SecurityEmailDeliveryFailed"),
            "Không thể gửi email bảo mật tới tên miền {EmailDomain}.");

    private static readonly Action<ILogger, string, int, string, SmtpStatusCode?, Exception?> LogSmtpFailure =
        LoggerMessage.Define<string, int, string, SmtpStatusCode?>(
            LogLevel.Error,
            new EventId(1002, "SmtpDeliveryFailed"),
            "SMTP gửi email thất bại. Host: {SmtpHost}, Port: {SmtpPort}, User: {SmtpUser}, StatusCode: {SmtpStatusCode}.");

    private readonly EmailOptions _options;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailOptions> options, ILogger<EmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> SendOtpAsync(
        string recipientEmail,
        string recipientName,
        string otpCode,
        DateTime expiresAtUtc)
    {
        return await SendMessageAsync(
            recipientEmail,
            "Mã xác minh tài khoản",
            BuildOtpBody(recipientName, otpCode, expiresAtUtc, false));
    }

    public async Task<bool> SendPasswordResetOtpAsync(
        string recipientEmail,
        string recipientName,
        string otpCode,
        DateTime expiresAtUtc)
    {
        return await SendMessageAsync(
            recipientEmail,
            "Mã đặt lại mật khẩu",
            BuildOtpBody(recipientName, otpCode, expiresAtUtc, true));
    }

    public async Task<bool> SendPasswordChangedAsync(
        string recipientEmail,
        string recipientName,
        DateTime changedAtUtc)
    {
        var safeName = WebUtility.HtmlEncode(recipientName);
        var changedAt = changedAtUtc.ToString(
            "HH:mm 'UTC' dd/MM/yyyy",
            System.Globalization.CultureInfo.InvariantCulture);
        var body = $$"""
            <!doctype html>
            <html lang="vi">
            <body style="font-family:Arial,sans-serif;color:#111;line-height:1.6">
              <p>Xin chào {{safeName}},</p>
              <p>Mật khẩu ProjectMgmt của bạn đã được thay đổi lúc {{changedAt}}.</p>
              <p>Tất cả phiên đăng nhập cũ đã bị thu hồi.</p>
              <p>Nếu bạn không thực hiện thay đổi này, hãy liên hệ quản trị viên ngay.</p>
            </body>
            </html>
            """;

        return await SendMessageAsync(recipientEmail, "Mật khẩu đã được thay đổi", body);
    }

    public Task<bool> SendProjectInvitationAsync(
        string recipientEmail,
        string recipientName,
        Guid projectId,
        string? roleName)
    {
        return SendMessageAsync(
            recipientEmail,
            "Bạn đã được thêm vào dự án",
            BuildProjectMembershipBody(
                recipientName,
                projectId,
                roleName,
                "Bạn đã được thêm vào một dự án",
                "Vai trò được giao"));
    }

    public Task<bool> SendProjectRoleChangedAsync(
        string recipientEmail,
        string recipientName,
        Guid projectId,
        string? roleName)
    {
        return SendMessageAsync(
            recipientEmail,
            "Vai trò dự án của bạn đã thay đổi",
            BuildProjectMembershipBody(
                recipientName,
                projectId,
                roleName,
                "Vai trò của bạn trong dự án đã được thay đổi",
                "Vai trò mới"));
    }

    public Task<bool> SendProjectRoleRevokedAsync(
        string recipientEmail,
        string recipientName,
        Guid projectId)
    {
        return SendMessageAsync(
            recipientEmail,
            "Quyền truy cập dự án đã được thu hồi",
            BuildProjectMembershipBody(
                recipientName,
                projectId,
                null,
                "Bạn đã được gỡ khỏi dự án",
                null));
    }

    public Task<bool> SendInvitationAcceptedAsync(
        string recipientEmail,
        string recipientName,
        string memberName,
        string memberEmail,
        Guid projectId)
    {
        var safeRecipient = WebUtility.HtmlEncode(recipientName);
        var safeMember = WebUtility.HtmlEncode(memberName);
        var safeEmail = WebUtility.HtmlEncode(memberEmail);
        var body = $$"""
            <!doctype html>
            <html lang="vi">
            <body style="font-family:Arial,sans-serif;color:#111;line-height:1.6">
              <p>Xin chào {{safeRecipient}},</p>
              <p>Thành viên <strong>{{safeMember}}</strong> ({{safeEmail}}) đã <strong>chấp nhận</strong> lời mời tham gia dự án của bạn.</p>
              <p>Mã dự án: <strong>{{projectId}}</strong>.</p>
              <p>Họ đã chính thức tham gia vào nhóm làm việc trên hệ thống.</p>
            </body>
            </html>
            """;

        return SendMessageAsync(
            recipientEmail,
            "Lời mời tham gia dự án đã được chấp nhận",
            body);
    }

    public Task<bool> SendInvitationDeclinedAsync(
        string recipientEmail,
        string recipientName,
        string memberName,
        string memberEmail,
        Guid projectId)
    {
        var safeRecipient = WebUtility.HtmlEncode(recipientName);
        var safeMember = WebUtility.HtmlEncode(memberName);
        var safeEmail = WebUtility.HtmlEncode(memberEmail);
        var body = $$"""
            <!doctype html>
            <html lang="vi">
            <body style="font-family:Arial,sans-serif;color:#111;line-height:1.6">
              <p>Xin chào {{safeRecipient}},</p>
              <p>Thành viên <strong>{{safeMember}}</strong> ({{safeEmail}}) đã <strong>từ chối</strong> lời mời tham gia dự án.</p>
              <p>Mã dự án: <strong>{{projectId}}</strong>.</p>
              <p>Bạn có thể liên hệ trực tiếp với thành viên hoặc gửi lại lời mời nếu cần thiết.</p>
            </body>
            </html>
            """;

        return SendMessageAsync(
            recipientEmail,
            "Lời mời tham gia dự án đã bị từ chối",
            body);
    }

    public Task<bool> SendOrganizationInvitationAsync(
        string recipientEmail,
        string recipientName,
        string orgName,
        string inviterName,
        string roleName,
        string inviteAcceptUrl)
    {
        var safeRecipient = WebUtility.HtmlEncode(recipientName);
        var safeOrg = WebUtility.HtmlEncode(orgName);
        var safeInviter = WebUtility.HtmlEncode(inviterName);
        var safeRole = WebUtility.HtmlEncode(roleName);
        var safeUrl = WebUtility.HtmlEncode(inviteAcceptUrl);
        const string notificationsUrl = "http://projectmgmt_develop.dev.com/notifications";
        const string localNotificationsUrl = "http://localhost:4200/notifications";

        var body = $$"""
            <!doctype html>
            <html lang="vi">
            <body style="font-family:Arial,sans-serif;color:#0f172a;line-height:1.6;background:#f8fafc;padding:24px">
              <div style="max-width:560px;margin:0 auto;background:#ffffff;border:1px solid #e2e8f0;border-radius:12px;padding:28px">
                <h2 style="margin:0 0 12px 0;color:#0f172a;font-size:20px">Lời mời tham gia tổ chức {{safeOrg}}</h2>
                <p style="margin:0 0 12px 0">Xin chào <strong>{{safeRecipient}}</strong>,</p>
                <p style="margin:0 0 12px 0">
                  <strong>{{safeInviter}}</strong> đã mời bạn tham gia tổ chức <strong>{{safeOrg}}</strong> trên Hệ thống Quản lý Dự án Scrum tích hợp AI với vai trò <strong>{{safeRole}}</strong>.
                </p>
                <div style="margin:24px 0">
                  <a href="{{safeUrl}}" style="display:inline-block;background:#0f172a;color:#ffffff;text-decoration:none;font-weight:700;padding:12px 22px;border-radius:8px;margin-right:10px">
                    Chấp nhận &amp; Tham gia tổ chức
                  </a>
                  <a href="{{notificationsUrl}}" style="display:inline-block;background:#f1f5f9;color:#0f172a;text-decoration:none;font-weight:600;padding:12px 18px;border-radius:8px;border:1px solid #cbd5e1">
                    Mở Trung tâm Thông báo
                  </a>
                </div>
                <p style="margin:12px 0 4px 0;font-size:13px;color:#475569">
                  Link tham gia trực tiếp: <a href="{{safeUrl}}" style="color:#2563eb">{{safeUrl}}</a>
                </p>
                <p style="margin:4px 0 0 0;font-size:13px;color:#64748b">
                  Hoặc truy cập <a href="{{notificationsUrl}}" style="color:#2563eb">{{notificationsUrl}}</a> (Localhost: <a href="{{localNotificationsUrl}}" style="color:#2563eb">{{localNotificationsUrl}}</a>) để xác nhận trong mục Thông báo hoặc Trang chủ.
                </p>
              </div>
            </body>
            </html>
            """;

        return SendMessageAsync(
            recipientEmail,
            $"Lời mời tham gia tổ chức {orgName}",
            body);
    }

    private async Task<bool> SendMessageAsync(
        string recipientEmail,
        string subject,
        string body)
    {
        try
        {
            ValidateConfiguration();

            using var message = new MailMessage
            {
                From = new MailAddress(_options.From, _options.FromName, Encoding.UTF8),
                Subject = subject,
                SubjectEncoding = Encoding.UTF8,
                BodyEncoding = Encoding.UTF8,
                IsBodyHtml = true,
                Body = body
            };
            message.To.Add(new MailAddress(recipientEmail));

            using var smtpClient = new SmtpClient(_options.SmtpHost, _options.Port)
            {
                EnableSsl = _options.EnableSsl,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_options.User, _options.Pass),
                Timeout = _options.TimeoutMilliseconds,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            await smtpClient.SendMailAsync(message);
            return true;
        }
        catch (Exception exception)
            when (exception is SmtpException or InvalidOperationException or FormatException)
        {
            LogSecurityEmailDeliveryFailure(_logger, GetEmailDomain(recipientEmail), exception);
            var smtpException = exception as SmtpException;
            LogSmtpFailure(
                _logger,
                _options.SmtpHost,
                _options.Port,
                MaskEmail(_options.User),
                smtpException?.StatusCode,
                exception);
            if (_options.FallbackToLogOnFailure)
            {
                _logger.LogWarning("Email delivery failed to {Recipient}. FallbackToLogOnFailure is enabled: treating as delivered for development. Subject: {Subject}", recipientEmail, subject);
                return true;
            }
            return false;
        }
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.SmtpHost)
            || _options.Port is < 1 or > 65_535
            || string.IsNullOrWhiteSpace(_options.User)
            || string.IsNullOrWhiteSpace(_options.Pass)
            || string.IsNullOrWhiteSpace(_options.From))
        {
            throw new InvalidOperationException(
                "Email SMTP is not configured. Set Email:SmtpHost, Port, User, Pass and From.");
        }
    }

    private static string MaskEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        return atIndex > 1
            ? $"{email[0]}***{email[(atIndex - 1)..]}"
            : "***";
    }

    private static string BuildOtpBody(
        string recipientName,
        string otpCode,
        DateTime expiresAtUtc,
        bool isPasswordReset)
    {
        var safeName = WebUtility.HtmlEncode(recipientName);
        var safeCode = WebUtility.HtmlEncode(otpCode);
        var expiry = expiresAtUtc.ToString("HH:mm 'UTC' dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
        var action = isPasswordReset ? "đặt lại mật khẩu" : "xác minh tài khoản";
        var ignoredAction = isPasswordReset ? "đặt lại mật khẩu" : "đăng ký";

        return $$"""
            <!doctype html>
            <html lang="vi">
            <body style="font-family:Arial,sans-serif;color:#111;line-height:1.6">
              <p>Xin chào {{safeName}},</p>
              <p>Mã {{action}} của bạn là:</p>
              <p style="font-size:28px;font-weight:700;letter-spacing:8px">{{safeCode}}</p>
              <p>Mã hết hạn lúc {{expiry}}. Không chia sẻ mã này cho bất kỳ ai.</p>
              <p>Nếu bạn không yêu cầu {{ignoredAction}}, hãy bỏ qua email này.</p>
            </body>
            </html>
            """;
    }

    private static string BuildProjectMembershipBody(
        string recipientName,
        Guid projectId,
        string? roleName,
        string action,
        string? roleLabel)
    {
        var safeName = WebUtility.HtmlEncode(recipientName);
        var safeAction = WebUtility.HtmlEncode(action);
        var safeRole = WebUtility.HtmlEncode(roleName);
        var safeRoleLabel = WebUtility.HtmlEncode(roleLabel);
        var roleParagraph = string.IsNullOrWhiteSpace(safeRole)
            ? string.Empty
            : $"<p>{safeRoleLabel}: <strong>{safeRole}</strong>.</p>";

        return $$"""
            <!doctype html>
            <html lang="vi">
            <body style="font-family:Arial,sans-serif;color:#0f172a;line-height:1.6;background:#f8fafc;padding:24px">
              <div style="max-width:560px;margin:0 auto;background:#ffffff;border:1px solid #e2e8f0;border-radius:12px;padding:28px">
                <h2 style="margin:0 0 12px 0;color:#0f172a;font-size:20px">{{safeAction}}</h2>
                <p>Xin chào <strong>{{safeName}}</strong>,</p>
                <p>{{safeAction}} trên Hệ thống Quản lý Dự án Scrum tích hợp AI.</p>
                <p>Mã dự án: <strong>{{projectId}}</strong>.</p>
                {{roleParagraph}}
                <div style="margin:24px 0">
                  <a href="http://projectmgmt_develop.dev.com/projects/{{projectId}}/summary" style="display:inline-block;background:#0f172a;color:#ffffff;text-decoration:none;font-weight:700;padding:12px 22px;border-radius:8px;margin-right:10px">
                    Mở Dự án ngay
                  </a>
                  <a href="http://projectmgmt_develop.dev.com/notifications" style="display:inline-block;background:#f1f5f9;color:#0f172a;text-decoration:none;font-weight:600;padding:12px 18px;border-radius:8px;border:1px solid #cbd5e1">
                    Xem Thông báo
                  </a>
                </div>
                <p style="font-size:13px;color:#64748b">Nếu bạn cho rằng đây là nhầm lẫn, hãy liên hệ quản trị viên dự án.</p>
              </div>
            </body>
            </html>
            """;
    }

    private static string GetEmailDomain(string email)
    {
        var separatorIndex = email.LastIndexOf('@');
        return separatorIndex >= 0 ? email[(separatorIndex + 1)..] : "unknown";
    }
}

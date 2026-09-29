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

    private static string GetEmailDomain(string email)
    {
        var separatorIndex = email.LastIndexOf('@');
        return separatorIndex >= 0 ? email[(separatorIndex + 1)..] : "unknown";
    }
}

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
    private static readonly Action<ILogger, string, Exception?> LogOtpDeliveryFailure =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(1001, "OtpEmailDeliveryFailed"),
            "Không thể gửi email OTP tới tên miền {EmailDomain}.");

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
        try
        {
            ValidateConfiguration();

            using var message = new MailMessage
            {
                From = new MailAddress(_options.From, _options.FromName, Encoding.UTF8),
                Subject = "Mã xác minh tài khoản",
                SubjectEncoding = Encoding.UTF8,
                BodyEncoding = Encoding.UTF8,
                IsBodyHtml = true,
                Body = BuildOtpBody(recipientName, otpCode, expiresAtUtc)
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
            LogOtpDeliveryFailure(_logger, GetEmailDomain(recipientEmail), exception);
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

    private static string BuildOtpBody(string recipientName, string otpCode, DateTime expiresAtUtc)
    {
        var safeName = WebUtility.HtmlEncode(recipientName);
        var safeCode = WebUtility.HtmlEncode(otpCode);
        var expiry = expiresAtUtc.ToString("HH:mm 'UTC' dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);

        return $$"""
            <!doctype html>
            <html lang="vi">
            <body style="font-family:Arial,sans-serif;color:#111;line-height:1.6">
              <p>Xin chào {{safeName}},</p>
              <p>Mã xác minh tài khoản của bạn là:</p>
              <p style="font-size:28px;font-weight:700;letter-spacing:8px">{{safeCode}}</p>
              <p>Mã hết hạn lúc {{expiry}}. Không chia sẻ mã này cho bất kỳ ai.</p>
              <p>Nếu bạn không yêu cầu đăng ký, hãy bỏ qua email này.</p>
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

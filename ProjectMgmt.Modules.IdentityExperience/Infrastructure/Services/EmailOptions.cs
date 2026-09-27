namespace IdentityExperience.Infrastructure.Services;

public class EmailOptions
{
    public string SmtpHost { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string User { get; set; } = string.Empty;
    public string Pass { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string FromName { get; set; } = "Hệ thống Quản lý Dự án Scrum tích hợp AI";
    public bool EnableSsl { get; set; } = true;
    public int TimeoutMilliseconds { get; set; } = 15_000;
}

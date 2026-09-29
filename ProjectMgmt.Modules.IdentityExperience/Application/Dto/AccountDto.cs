namespace IdentityExperience.Application.Dto;

/// <summary>
/// DTO dùng chung cho các lệnh xác thực. Mỗi API chỉ đọc những trường nó cần.
/// </summary>
public class AccountDto
{
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Code { get; set; }
    public string? OtpCode { get; set; }
    public string? Purpose { get; set; }
    public string? RefreshToken { get; set; }
    public string? CurrentPassword { get; set; }
    public string? NewPassword { get; set; }
}

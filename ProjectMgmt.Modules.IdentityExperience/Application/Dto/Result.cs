namespace IdentityExperience.Application.Dto;

public class Result
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? ErrorCode { get; set; }
}

public class RegisterResult : Result
{
    public Guid? UserId { get; set; }
    public string? Email { get; set; }
    public string? Status { get; set; }
    public DateTime? OtpExpiresAt { get; set; }
    public int? ResendAfterSeconds { get; set; }
}

public class OtpResult : Result
{
    public string? Email { get; set; }
    public string? Status { get; set; }
    public DateTime? OtpExpiresAt { get; set; }
    public int? ResendAfterSeconds { get; set; }
    public int? AttemptsRemaining { get; set; }
}

public class ResultLogin : Result
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? AccessTokenExpiresAt { get; set; }
    public DateTime? RefreshTokenExpiresAt { get; set; }
    public Guid? UserId { get; set; }
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public List<string>? Roles { get; set; }
}

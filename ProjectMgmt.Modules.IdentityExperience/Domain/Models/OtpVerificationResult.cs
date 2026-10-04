namespace IdentityExperience.Domain.Models;

public enum OtpVerificationStatus
{
    Verified,
    AlreadyVerified,
    UserNotFound,
    AccountDisabled,
    NoActiveCode,
    Expired,
    InvalidCode,
    AttemptsExceeded
}

public class OtpVerificationResult
{
    public OtpVerificationStatus Status { get; set; }
    public int? AttemptsRemaining { get; set; }
}

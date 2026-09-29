namespace IdentityExperience.Domain.Models;

public enum PasswordResetStatus
{
    Changed,
    UserNotFound,
    AccountDisabled,
    EmailNotVerified,
    NoActiveCode,
    Expired,
    InvalidCode,
    AttemptsExceeded
}

public class PasswordResetResult
{
    public PasswordResetStatus Status { get; set; }
    public int? AttemptsRemaining { get; set; }
}

public enum PasswordChangeStatus
{
    Changed,
    UserNotFound,
    AccountDisabled,
    EmailNotVerified,
    CurrentPasswordChanged
}

public class PasswordChangeResult
{
    public PasswordChangeStatus Status { get; set; }
}

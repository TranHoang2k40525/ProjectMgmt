using IdentityExperience.Domain.Entities;
using IdentityExperience.Domain.Models;

namespace IdentityExperience.Domain.IRepositories;

public interface IIdentityRepository
{
    Task<User?> GetUserByNormalizedEmailAsync(
        string normalizedEmail,
        bool tracking = false);

    Task<UserProfile?> GetUserProfileAsync(
        Guid userId,
        bool tracking = false);

    Task<bool> PhoneNumberExistsAsync(string phoneNumber);

    /// <summary>
    /// Lưu User, UserProfile và OtpCode trong cùng một transaction cơ sở dữ liệu.
    /// Trả về false khi email hoặc số điện thoại bị trùng do yêu cầu đồng thời.
    /// </summary>
    Task<bool> CreatePendingRegistrationAsync(
        User user,
        UserProfile profile,
        OtpCode otpCode);

    /// <summary>
    /// Khóa tài khoản, kiểm tra cooldown, vô hiệu OTP cũ và lưu OTP mới trong một transaction.
    /// </summary>
    Task<OtpIssueResult> ReplaceEmailVerificationOtpAsync(
        Guid userId,
        OtpCode newOtpCode,
        DateTime nowUtc,
        TimeSpan minimumInterval);

    /// <summary>
    /// Khóa tài khoản và xác minh OTP; trạng thái OTP và User được cập nhật nguyên tử.
    /// </summary>
    Task<OtpVerificationResult> VerifyEmailOtpAsync(
        Guid userId,
        string purpose,
        string expectedCodeHash,
        DateTime nowUtc,
        int maximumAttempts);

    Task<List<string>> GetSystemRoleNamesAsync(Guid userId);

    Task<LoginSessionStatus> CreateLoginSessionAsync(
        Guid userId,
        RefreshToken refreshToken,
        DateTime nowUtc);

    Task<AuthSessionContext?> GetRefreshSessionContextAsync(string refreshTokenHash);

    Task<TokenRotationStatus> RotateRefreshTokenAsync(
        string currentTokenHash,
        RefreshToken replacementToken,
        DateTime nowUtc);

    Task<bool> RevokeRefreshTokenAsync(string refreshTokenHash);
}

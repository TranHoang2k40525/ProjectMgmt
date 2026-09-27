using IdentityExperience.Domain.Entities;

namespace IdentityExperience.Domain.IRepositories;

public interface IIdentityRepository
{
    Task<User?> GetUserByNormalizedEmailAsync(
        string normalizedEmail,
        bool tracking = false,
        CancellationToken cancellationToken = default);

    Task<UserProfile?> GetUserProfileAsync(
        Guid userId,
        bool tracking = false,
        CancellationToken cancellationToken = default);

    Task<bool> PhoneNumberExistsAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lưu User, UserProfile và OtpCode trong cùng một transaction cơ sở dữ liệu.
    /// Trả về false khi email hoặc số điện thoại bị trùng do yêu cầu đồng thời.
    /// </summary>
    Task<bool> CreatePendingRegistrationAsync(
        User user,
        UserProfile profile,
        OtpCode otpCode,
        CancellationToken cancellationToken = default);
}

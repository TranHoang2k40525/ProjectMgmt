using IdentityExperience.Domain.Models;

namespace IdentityExperience.Domain.IRepositories;

public interface IProfileRepository
{
    Task<ProfileDetails?> GetProfileDetailsAsync(Guid userId);

    Task<ProfileUpdateStatus> UpdateProfileAsync(
        Guid userId,
        ProfileUpdateValues values,
        DateTime nowUtc);

    Task<AvatarUpdateResult> UpdateAvatarUrlAsync(
        Guid userId,
        string avatarUrl,
        DateTime nowUtc);
}

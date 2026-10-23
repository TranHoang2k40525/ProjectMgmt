using IdentityExperience.Application.Dto;

namespace IdentityExperience.Application.IServices;

public interface IProfileServices
{
    Task<ProfileDto> GetMyProfileAsync(Guid userId);
    Task<ProfileDto> UpdateMyProfileAsync(Guid userId, ProfileDto request);
    Task<AvatarResult> UpdateAvatarAsync(
        Guid userId,
        Stream content,
        long length,
        string? contentType);
}

public interface IAvatarStorage
{
    Task<string> SaveAsync(Guid userId, Stream content, string extension);
    Task DeleteAsync(string? avatarUrl);
}

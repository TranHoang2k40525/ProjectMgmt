using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;
using IdentityExperience.Application.Services;
using IdentityExperience.Domain.Entities;
using IdentityExperience.Domain.IRepositories;
using IdentityExperience.Domain.Models;
using Xunit;

namespace ProjectMgmt.Tests;

public class IdentityProfileSkillTests
{
    private static readonly DateTime TestNowUtc = new(2026, 9, 30, 2, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ProfileUpdateKeepsMissingFieldsAndNormalizesVietnamesePhone()
    {
        var userId = Guid.NewGuid();
        var repository = new FakeProfileRepository(CreateProfileDetails(userId));
        var service = new ProfileServices(
            repository,
            new FakeAvatarStorage(),
            new FixedTimeProvider(TestNowUtc));

        var result = await service.UpdateMyProfileAsync(userId, new ProfileDto
        {
            FullName = "  Trần Văn Hoàng  ",
            PhoneNumber = "0901234567",
            JobTitle = "Backend Engineer"
        });

        Assert.True(result.Success);
        Assert.NotNull(repository.UpdatedValues);
        Assert.Equal("Trần Văn Hoàng", repository.UpdatedValues.DisplayName);
        Assert.Equal("+84901234567", repository.UpdatedValues.PhoneNumber);
        Assert.Equal("UTC", repository.UpdatedValues.Timezone);
        Assert.Equal("Backend Engineer", repository.UpdatedValues.JobTitle);
    }

    [Fact]
    public async Task AvatarRejectsFileWhoseBytesAreNotAnAllowedImage()
    {
        var repository = new FakeProfileRepository(CreateProfileDetails(Guid.NewGuid()));
        var storage = new FakeAvatarStorage();
        var service = new ProfileServices(
            repository,
            storage,
            new FixedTimeProvider(TestNowUtc));
        await using var content = new MemoryStream("not-an-image"u8.ToArray());

        var result = await service.UpdateAvatarAsync(
            repository.Details!.User.Id,
            content,
            content.Length,
            "image/png");

        Assert.False(result.Success);
        Assert.Equal("IDENTITY_AVATAR_TYPE_INVALID", result.ErrorCode);
        Assert.Null(storage.SavedUrl);
    }

    [Fact]
    public async Task AvatarStoresValidSignatureAndRemovesPreviousManagedFile()
    {
        var userId = Guid.NewGuid();
        var repository = new FakeProfileRepository(CreateProfileDetails(userId))
        {
            AvatarResult = new AvatarUpdateResult
            {
                Updated = true,
                PreviousAvatarUrl = "/assets/avatars/old.png"
            }
        };
        var storage = new FakeAvatarStorage();
        var service = new ProfileServices(
            repository,
            storage,
            new FixedTimeProvider(TestNowUtc));
        byte[] pngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        await using var content = new MemoryStream(pngHeader);

        var result = await service.UpdateAvatarAsync(
            userId,
            content,
            content.Length,
            "application/octet-stream");

        Assert.True(result.Success);
        Assert.EndsWith(".png", result.AvatarUrl, StringComparison.Ordinal);
        Assert.Equal("/assets/avatars/old.png", storage.DeletedUrl);
    }

    [Fact]
    public async Task SkillUpdateRejectsDuplicateBeforeWriting()
    {
        var skillId = Guid.NewGuid();
        var repository = new FakeSkillRepository();
        var service = new SkillServices(repository, new FixedTimeProvider(TestNowUtc));

        var result = await service.UpdateMySkillsAsync(Guid.NewGuid(),
        [
            new SkillDto { SkillId = skillId, ProficiencyLevel = 3 },
            new SkillDto { SkillId = skillId, ProficiencyLevel = 4 }
        ]);

        Assert.False(result.Success);
        Assert.Equal("IDENTITY_SKILL_DUPLICATED", result.ErrorCode);
        Assert.Null(repository.WrittenSkills);
    }

    [Fact]
    public async Task SelfUpdatingSkillsAlwaysMarksThemAsSelfDeclared()
    {
        var repository = new FakeSkillRepository();
        var service = new SkillServices(repository, new FixedTimeProvider(TestNowUtc));
        var userId = Guid.NewGuid();

        var result = await service.UpdateMySkillsAsync(userId,
        [
            new SkillDto
            {
                SkillId = Guid.NewGuid(),
                ProficiencyLevel = 5,
                YearsOfExperience = 4.5m,
                Verified = true
            }
        ]);

        Assert.True(result.Success);
        Assert.Equal(1, result.UpdatedCount);
        Assert.NotNull(repository.WrittenSkills);
        Assert.True(repository.WrittenSkills[0].IsSelfDeclared);
    }

    private static ProfileDetails CreateProfileDetails(Guid userId)
    {
        return new ProfileDetails
        {
            User = new User
            {
                Id = userId,
                Email = "member@example.com",
                NormalizedEmail = "MEMBER@EXAMPLE.COM",
                IsActive = true,
                IsEmailVerified = true,
                SecurityStamp = Guid.NewGuid()
            },
            Profile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                DisplayName = "Thành viên",
                PhoneNumber = "+84987654321",
                Timezone = "UTC",
                Bio = "Bio"
            }
        };
    }
}

public class FakeProfileRepository : IProfileRepository
{
    public FakeProfileRepository(ProfileDetails? details)
    {
        Details = details;
    }

    public ProfileDetails? Details { get; set; }
    public ProfileUpdateValues? UpdatedValues { get; private set; }
    public AvatarUpdateResult AvatarResult { get; set; } = new() { Updated = true };

    public Task<ProfileDetails?> GetProfileDetailsAsync(Guid userId)
    {
        if (Details is not null && UpdatedValues is not null)
        {
            Details.Profile.DisplayName = UpdatedValues.DisplayName;
            Details.Profile.PhoneNumber = UpdatedValues.PhoneNumber;
            Details.Profile.Timezone = UpdatedValues.Timezone;
            Details.Profile.JobTitle = UpdatedValues.JobTitle;
            Details.Profile.SeniorityLevel = UpdatedValues.SeniorityLevel;
            Details.Profile.YearsOfExperience = UpdatedValues.YearsOfExperience;
            Details.Profile.Bio = UpdatedValues.Bio;
        }

        return Task.FromResult(Details);
    }

    public Task<ProfileUpdateStatus> UpdateProfileAsync(
        Guid userId,
        ProfileUpdateValues values,
        DateTime nowUtc)
    {
        UpdatedValues = values;
        return Task.FromResult(ProfileUpdateStatus.Updated);
    }

    public Task<AvatarUpdateResult> UpdateAvatarUrlAsync(
        Guid userId,
        string avatarUrl,
        DateTime nowUtc)
    {
        return Task.FromResult(AvatarResult);
    }
}

public class FakeAvatarStorage : IAvatarStorage
{
    public string? SavedUrl { get; private set; }
    public string? DeletedUrl { get; private set; }

    public Task<string> SaveAsync(Guid userId, Stream content, string extension)
    {
        SavedUrl = $"/assets/avatars/{userId:N}{extension}";
        return Task.FromResult(SavedUrl);
    }

    public Task DeleteAsync(string? avatarUrl)
    {
        DeletedUrl = avatarUrl;
        return Task.CompletedTask;
    }
}

public class FakeSkillRepository : ISkillRepository
{
    public List<UserSkill>? WrittenSkills { get; private set; }

    public Task<List<SkillCatalog>> GetCatalogAsync(string? category, string? searchQuery)
    {
        return Task.FromResult(new List<SkillCatalog>());
    }

    public Task<(SkillWriteStatus Status, SkillCatalog? Skill)> CreateCatalogSkillAsync(
        SkillCatalog skill)
    {
        return Task.FromResult<(SkillWriteStatus, SkillCatalog?)>((SkillWriteStatus.Created, skill));
    }

    public Task<bool> UserExistsAsync(Guid userId)
    {
        return Task.FromResult(true);
    }

    public Task<List<UserSkillDetails>> GetUserSkillsAsync(Guid userId)
    {
        return Task.FromResult(new List<UserSkillDetails>());
    }

    public Task<(SkillWriteStatus Status, int UpdatedCount)> ReplaceUserSkillsAsync(
        Guid userId,
        List<UserSkill> skills,
        DateTime nowUtc)
    {
        WrittenSkills = skills;
        return Task.FromResult((SkillWriteStatus.Updated, skills.Count));
    }

    public Task<SkillWriteStatus> VerifyUserSkillAsync(
        Guid userId,
        Guid skillId,
        bool verified,
        sbyte? level,
        DateTime nowUtc)
    {
        return Task.FromResult(SkillWriteStatus.Updated);
    }
}

using Microsoft.EntityFrameworkCore;
using ProjectMgmt.BuildingBlocks.Persistence;
using ProjectMgmt.Modules.IdentityExperience.AiAssist.Domain.Entities;
using ProjectMgmt.Modules.IdentityExperience.IdentityAccess.Domain.Entities;
using NotificationEntity = ProjectMgmt.Modules.IdentityExperience.Notification.Domain.Entities.Notification;

namespace ProjectMgmt.Modules.IdentityExperience.Infrastructure.Persistence;

public sealed class IdentityExperienceDbContext(DbContextOptions<IdentityExperienceDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<SkillCatalog> Skills => Set<SkillCatalog>();
    public DbSet<UserSkill> UserSkills => Set<UserSkill>();
    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>();
    public DbSet<AiGenerationLog> AiGenerationLogs => Set<AiGenerationLog>();
    public DbSet<AiSuggestedTask> AiSuggestedTasks => Set<AiSuggestedTask>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityExperienceDbContext).Assembly);
        modelBuilder.ApplyProjectMgmtColumnConventions();
    }
}

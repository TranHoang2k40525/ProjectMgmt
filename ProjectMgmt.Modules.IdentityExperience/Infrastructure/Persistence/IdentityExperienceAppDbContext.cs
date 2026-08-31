using Microsoft.EntityFrameworkCore;
using ProjectMgmt.Modules.IdentityExperience.AiAssist.Domain.Entities;
using ProjectMgmt.Modules.IdentityExperience.IdentityAccess.Domain.Entities;
using NotificationEntity = ProjectMgmt.Modules.IdentityExperience.Notification.Domain.Entities.Notification;

namespace ProjectMgmt.Modules.IdentityExperience.Infrastructure.Persistence;

public class IdentityExperienceAppDbContext : DbContext
{
    public IdentityExperienceAppDbContext(DbContextOptions<IdentityExperienceAppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<UserProfile> UserProfiles { get; set; } = null!;
    public DbSet<ExternalLogin> ExternalLogins { get; set; } = null!;
    public DbSet<OtpCode> OtpCodes { get; set; } = null!;
    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
    public DbSet<Role> Roles { get; set; } = null!;
    public DbSet<Permission> Permissions { get; set; } = null!;
    public DbSet<RolePermission> RolePermissions { get; set; } = null!;
    public DbSet<UserRole> UserRoles { get; set; } = null!;
    public DbSet<SkillCatalog> Skills { get; set; } = null!;
    public DbSet<UserSkill> UserSkills { get; set; } = null!;
    public DbSet<NotificationEntity> Notifications { get; set; } = null!;
    public DbSet<AiGenerationLog> AiGenerationLogs { get; set; } = null!;
    public DbSet<AiSuggestedTask> AiSuggestedTasks { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityExperienceAppDbContext).Assembly);
        ApplyColumnConventions(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    private static void ApplyColumnConventions(ModelBuilder modelBuilder)
    {
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(entity => entity.GetProperties()))
        {
            var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
            if (type == typeof(Guid))
            {
                property.SetColumnType("char(36)");
            }
            else if (type == typeof(DateTime))
            {
                property.SetColumnType("datetime(6)");
            }
            else if (type == typeof(DateOnly))
            {
                property.SetColumnType("date");
            }
        }
    }
}

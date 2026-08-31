using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectMgmt.Modules.IdentityExperience.IdentityAccess.Domain.Entities;

namespace ProjectMgmt.Modules.IdentityExperience.IdentityAccess.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("User");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.Property(x => x.NormalizedEmail).HasMaxLength(256).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(255);
        builder.Property(x => x.IsEmailVerified).HasDefaultValue(false);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.Property(x => x.UpdatedAt).ValueGeneratedOnAddOrUpdate();
        builder.HasIndex(x => x.NormalizedEmail).IsUnique().HasDatabaseName("UQ_User_NormalizedEmail");
    }
}

internal sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("UserProfile");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DisplayName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.AvatarUrl).HasMaxLength(500);
        builder.Property(x => x.PhoneNumber).HasMaxLength(30);
        builder.Property(x => x.Timezone).HasMaxLength(64).HasDefaultValue("Asia/Ho_Chi_Minh");
        builder.Property(x => x.JobTitle).HasMaxLength(150);
        builder.Property(x => x.SeniorityLevel).HasMaxLength(20);
        builder.Property(x => x.YearsOfExperience).HasPrecision(4, 1);
        builder.Property(x => x.Bio).HasColumnType("text");
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.Property(x => x.UpdatedAt).ValueGeneratedOnAddOrUpdate();
        builder.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("UQ_UserProfile_UserId");
        builder.HasOne<User>().WithOne().HasForeignKey<UserProfile>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ExternalLoginConfiguration : IEntityTypeConfiguration<ExternalLogin>
{
    public void Configure(EntityTypeBuilder<ExternalLogin> builder)
    {
        builder.ToTable("ExternalLogin");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Provider).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ProviderKey).HasMaxLength(255).IsRequired();
        builder.Property(x => x.LinkedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.HasIndex(x => new { x.Provider, x.ProviderKey }).IsUnique().HasDatabaseName("UQ_ExternalLogin_Provider");
        builder.HasIndex(x => x.UserId).HasDatabaseName("IX_ExternalLogin_UserId");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class OtpCodeConfiguration : IEntityTypeConfiguration<OtpCode>
{
    public void Configure(EntityTypeBuilder<OtpCode> builder)
    {
        builder.ToTable("OtpCode");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CodeHash).HasMaxLength(255).IsRequired();
        builder.Property(x => x.Purpose).HasMaxLength(30).IsRequired();
        builder.Property(x => x.IsUsed).HasDefaultValue(false);
        builder.Property(x => x.AttemptCount).HasDefaultValue(0);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.HasIndex(x => new { x.UserId, x.Purpose, x.IsUsed }).HasDatabaseName("IX_OtpCode_User_Purpose");
        builder.HasIndex(x => x.ExpiresAt).HasDatabaseName("IX_OtpCode_ExpiresAt");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshToken");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TokenHash).HasColumnType("char(64)").IsRequired();
        builder.Property(x => x.IsRevoked).HasDefaultValue(false);
        builder.Property(x => x.CreatedByIp).HasMaxLength(45);
        builder.Property(x => x.UserAgent).HasMaxLength(400);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("UQ_RefreshToken_Hash");
        builder.HasIndex(x => new { x.UserId, x.IsRevoked }).HasDatabaseName("IX_RefreshToken_UserId");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<RefreshToken>().WithMany().HasForeignKey(x => x.ReplacedByTokenId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Role");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Scope).HasMaxLength(20).IsRequired();
        builder.Property(x => x.IsSystem).HasDefaultValue(false);
        builder.Property(x => x.Description).HasMaxLength(255);
        builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("UQ_Role_Name");
    }
}

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permission");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(255);
        builder.Property(x => x.Grouping).HasMaxLength(50);
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UQ_Permission_Code");
    }
}

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermission");
        builder.HasKey(x => new { x.RoleId, x.PermissionId });
        builder.HasIndex(x => x.PermissionId).HasDatabaseName("IX_RolePermission_PermissionId");
        builder.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Permission>().WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRole");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ScopeType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.Property(x => x.ScopeKey)
            .HasComputedColumnSql("IFNULL(`ScopeId`, '00000000-0000-0000-0000-000000000000')", stored: true);
        builder.HasIndex(x => new { x.UserId, x.RoleId, x.ScopeType, x.ScopeKey }).IsUnique().HasDatabaseName("UQ_UserRole_Scoped");
        builder.HasIndex(x => new { x.UserId, x.ScopeType, x.ScopeId }).HasDatabaseName("IX_UserRole_Lookup");
        builder.HasIndex(x => new { x.ScopeType, x.ScopeId }).HasDatabaseName("IX_UserRole_Scope");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SkillCatalogConfiguration : IEntityTypeConfiguration<SkillCatalog>
{
    public void Configure(EntityTypeBuilder<SkillCatalog> builder)
    {
        builder.ToTable("SkillCatalog");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(50);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UQ_SkillCatalog_Code");
    }
}

internal sealed class UserSkillConfiguration : IEntityTypeConfiguration<UserSkill>
{
    public void Configure(EntityTypeBuilder<UserSkill> builder)
    {
        builder.ToTable("UserSkill");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProficiencyLevel).HasColumnType("tinyint").HasDefaultValue(3);
        builder.Property(x => x.YearsOfExperience).HasPrecision(4, 1);
        builder.Property(x => x.IsSelfDeclared).HasDefaultValue(true);
        builder.Property(x => x.UpdatedAt).ValueGeneratedOnAddOrUpdate();
        builder.HasIndex(x => new { x.UserId, x.SkillId }).IsUnique().HasDatabaseName("UQ_UserSkill");
        builder.HasIndex(x => new { x.SkillId, x.ProficiencyLevel }).HasDatabaseName("IX_UserSkill_SkillId");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<SkillCatalog>().WithMany().HasForeignKey(x => x.SkillId).OnDelete(DeleteBehavior.Cascade);
    }
}

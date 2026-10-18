using IdentityExperience.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityExperience.Infrastructure.Configurations;

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRole", table =>
        {
            table.HasComment("Gán vai trò có phạm vi, thay cho OrgMember/ProjectMember riêng");
            table.HasCheckConstraint("CK_UserRole_ScopeType", "`ScopeType` IN ('System','Organization','Project')");
        });

        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(entity => entity.Id).HasName("PRIMARY");

        builder.Property(entity => entity.Id)
            .HasColumnName("Id")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.UserId)
            .HasColumnName("UserId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.RoleId)
            .HasColumnName("RoleId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.ScopeType)
            .HasColumnName("ScopeType")
            .HasColumnType("varchar(20)")
            .HasComment("System / Organization / Project")
            .IsRequired();

        builder.Property(entity => entity.ScopeId)
            .HasColumnName("ScopeId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD: Id của Organization hoặc Project. NULL nếu ScopeType=System");

        builder.Property(entity => entity.GrantedBy)
            .HasColumnName("GrantedBy")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("Ai là người gán vai trò này");

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.Property(entity => entity.ScopeKey)
            .HasColumnName("ScopeKey")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComputedColumnSql("IFNULL(`ScopeId`, '00000000-0000-0000-0000-000000000000')", stored: true)
            .ValueGeneratedOnAddOrUpdate();
        builder.Property(entity => entity.ScopeKey).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

        builder.HasIndex(entity => new { entity.UserId, entity.RoleId, entity.ScopeType, entity.ScopeKey }, "UQ_UserRole_Scoped").IsUnique();

        builder.HasIndex(entity => new { entity.UserId, entity.ScopeType, entity.ScopeId }, "IX_UserRole_Lookup");

        builder.HasIndex(entity => new { entity.ScopeType, entity.ScopeId }, "IX_UserRole_Scope");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .HasConstraintName("FK_UserRole_User")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(entity => entity.RoleId)
            .HasConstraintName("FK_UserRole_Role")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

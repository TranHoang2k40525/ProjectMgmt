using IdentityExperience.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityExperience.Infrastructure.Configurations;

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermission", table =>
        {
            table.HasComment("N-N Role <-> Permission");
        });

        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(entity => new { entity.RoleId, entity.PermissionId }).HasName("PRIMARY");

        builder.Property(entity => entity.RoleId)
            .HasColumnName("RoleId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.PermissionId)
            .HasColumnName("PermissionId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.HasIndex(entity => entity.PermissionId, "IX_RolePermission_PermissionId");

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(entity => entity.RoleId)
            .HasConstraintName("FK_RolePermission_Role")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Permission>()
            .WithMany()
            .HasForeignKey(entity => entity.PermissionId)
            .HasConstraintName("FK_RolePermission_Permission")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

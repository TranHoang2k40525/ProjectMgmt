using IdentityExperience.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityExperience.Infrastructure.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Role", table =>
        {
            table.HasComment("Danh mục vai trò RBAC");
            table.HasCheckConstraint("CK_Role_Scope", "`Scope` IN ('System','Organization','Project')");
        });

        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(entity => entity.Id).HasName("PRIMARY");

        builder.Property(entity => entity.Id)
            .HasColumnName("Id")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.Name)
            .HasColumnName("Name")
            .HasColumnType("varchar(80)")
            .IsRequired();

        builder.Property(entity => entity.Scope)
            .HasColumnName("Scope")
            .HasColumnType("varchar(20)")
            .HasComment("Phạm vi mặc định: System / Organization / Project")
            .IsRequired();

        builder.Property(entity => entity.IsSystem)
            .HasColumnName("IsSystem")
            .HasColumnType("tinyint(1)")
            .HasComment("Vai trò hệ thống, không cho xóa")
            .HasDefaultValue(false);

        builder.Property(entity => entity.Description)
            .HasColumnName("Description")
            .HasColumnType("varchar(255)");

        builder.HasIndex(entity => entity.Name, "UQ_Role_Name").IsUnique();
    }
}

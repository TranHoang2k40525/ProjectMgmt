using IdentityExperience.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityExperience.Infrastructure.Configurations;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permission", table =>
        {
            table.HasComment("Danh mục hành động nhỏ nhất có thể cấp phép");
        });

        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(entity => entity.Id).HasName("PRIMARY");

        builder.Property(entity => entity.Id)
            .HasColumnName("Id")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.Code)
            .HasColumnName("Code")
            .HasColumnType("varchar(80)")
            .HasComment("issue.create, sprint.close, ai.breakdown.request...")
            .IsRequired();

        builder.Property(entity => entity.Description)
            .HasColumnName("Description")
            .HasColumnType("varchar(255)");

        builder.Property(entity => entity.Grouping)
            .HasColumnName("Grouping")
            .HasColumnType("varchar(50)")
            .HasComment("Nhóm hiển thị trên UI phân quyền");

        builder.HasIndex(entity => entity.Code, "UQ_Permission_Code").IsUnique();
    }
}

using IdentityExperience.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityExperience.Infrastructure.Configurations;

public class SkillCatalogConfiguration : IEntityTypeConfiguration<SkillCatalog>
{
    public void Configure(EntityTypeBuilder<SkillCatalog> builder)
    {
        builder.ToTable("SkillCatalog", table =>
        {
            table.HasComment("Danh mục kỹ năng chuẩn hóa, dùng chung cho user và issue");
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
            .HasColumnType("varchar(60)")
            .HasComment("dotnet, angular, sql, devops, testing...")
            .IsRequired();

        builder.Property(entity => entity.Name)
            .HasColumnName("Name")
            .HasColumnType("varchar(120)")
            .IsRequired();

        builder.Property(entity => entity.Category)
            .HasColumnName("Category")
            .HasColumnType("varchar(50)")
            .HasComment("Backend / Frontend / Database / QA / DevOps / Design");

        builder.Property(entity => entity.IsActive)
            .HasColumnName("IsActive")
            .HasColumnType("tinyint(1)")
            .HasDefaultValue(true);

        builder.HasIndex(entity => entity.Code, "UQ_SkillCatalog_Code").IsUnique();

        builder.HasIndex(entity => entity.Name, "IX_SkillCatalog_Name");

        builder.HasIndex(entity => new { entity.Category, entity.Name }, "IX_SkillCatalog_Category_Name");
    }
}

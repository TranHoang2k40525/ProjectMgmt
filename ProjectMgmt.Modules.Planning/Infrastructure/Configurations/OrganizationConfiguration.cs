using Planning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Planning.Infrastructure.Configurations;

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("Organization", table =>
        {
            table.HasComment("Workspace cấp cao nhất, tương đương Site trong Jira Cloud");
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
            .HasColumnType("varchar(200)")
            .IsRequired();

        builder.Property(entity => entity.Slug)
            .HasColumnName("Slug")
            .HasColumnType("varchar(80)")
            .HasComment("URL-friendly, dùng cho subdomain/route")
            .IsRequired();

        builder.Property(entity => entity.OwnerId)
            .HasColumnName("OwnerId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> User.Id");

        builder.Property(entity => entity.IsActive)
            .HasColumnName("IsActive")
            .HasColumnType("tinyint(1)")
            .HasDefaultValue(true);

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.Property(entity => entity.UpdatedAt)
            .HasColumnName("UpdatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("NULL ON UPDATE CURRENT_TIMESTAMP(6)");
        builder.Property(entity => entity.UpdatedAt).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

        builder.HasIndex(entity => entity.Slug, "UQ_Organization_Slug").IsUnique();

        builder.HasIndex(entity => entity.OwnerId, "IX_Organization_OwnerId");

        builder.HasIndex(entity => entity.Name, "IX_Organization_Name");
    }
}

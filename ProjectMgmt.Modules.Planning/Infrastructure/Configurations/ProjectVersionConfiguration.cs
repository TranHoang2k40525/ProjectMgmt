using Planning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Planning.Infrastructure.Configurations;

public class ProjectVersionConfiguration : IEntityTypeConfiguration<ProjectVersion>
{
    public void Configure(EntityTypeBuilder<ProjectVersion> builder)
    {
        builder.ToTable("ProjectVersion", table =>
        {
            table.HasComment("Tương đương Fix Version / Release của Jira");
        });

        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(entity => entity.Id).HasName("PRIMARY");

        builder.Property(entity => entity.Id)
            .HasColumnName("Id")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.ProjectId)
            .HasColumnName("ProjectId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.Name)
            .HasColumnName("Name")
            .HasColumnType("varchar(60)")
            .HasComment("v1.2.0")
            .IsRequired();

        builder.Property(entity => entity.Description)
            .HasColumnName("Description")
            .HasColumnType("varchar(500)");

        builder.Property(entity => entity.StartDate)
            .HasColumnName("StartDate")
            .HasColumnType("date");

        builder.Property(entity => entity.ReleaseDate)
            .HasColumnName("ReleaseDate")
            .HasColumnType("date");

        builder.Property(entity => entity.IsReleased)
            .HasColumnName("IsReleased")
            .HasColumnType("tinyint(1)")
            .HasDefaultValue(false);

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(entity => new { entity.ProjectId, entity.Name }, "UQ_ProjectVersion_Name").IsUnique();

        builder.HasIndex(entity => entity.Name, "IX_ProjectVersion_Name");

        builder.HasIndex(entity => new { entity.IsReleased, entity.ReleaseDate }, "IX_ProjectVersion_Release");

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(entity => entity.ProjectId)
            .HasConstraintName("FK_ProjectVersion_Project")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

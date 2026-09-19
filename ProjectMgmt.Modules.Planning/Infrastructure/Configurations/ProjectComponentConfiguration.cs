using Planning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Planning.Infrastructure.Configurations;

public class ProjectComponentConfiguration : IEntityTypeConfiguration<ProjectComponent>
{
    public void Configure(EntityTypeBuilder<ProjectComponent> builder)
    {
        builder.ToTable("ProjectComponent", table =>
        {
            table.HasComment("Tương đương Components của Jira");
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
            .HasColumnType("varchar(150)")
            .HasComment("Backend API, Mobile App, Database...")
            .IsRequired();

        builder.Property(entity => entity.Description)
            .HasColumnName("Description")
            .HasColumnType("varchar(500)");

        builder.Property(entity => entity.LeadUserId)
            .HasColumnName("LeadUserId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> User.Id. Dùng auto-suggest assignee");

        builder.Property(entity => entity.DefaultSkillId)
            .HasColumnName("DefaultSkillId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> SkillCatalog.Id. AI ASSIGNMENT: skill mặc định của component");

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(entity => new { entity.ProjectId, entity.Name }, "UQ_ProjectComponent_Name").IsUnique();

        builder.HasIndex(entity => entity.Name, "IX_ProjectComponent_Name");

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(entity => entity.ProjectId)
            .HasConstraintName("FK_ProjectComponent_Project")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

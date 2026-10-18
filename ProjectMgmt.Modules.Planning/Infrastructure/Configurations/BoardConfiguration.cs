using Planning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Planning.Infrastructure.Configurations;

public class BoardConfiguration : IEntityTypeConfiguration<Board>
{
    public void Configure(EntityTypeBuilder<Board> builder)
    {
        builder.ToTable("Board", table =>
        {
            table.HasComment("Một project có thể có nhiều board");
            table.HasCheckConstraint("CK_Board_Type", "`Type` IN ('Scrum','Kanban')");
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
            .IsRequired();

        builder.Property(entity => entity.Type)
            .HasColumnName("Type")
            .HasColumnType("varchar(20)")
            .HasComment("Scrum (gắn Sprint) hoặc Kanban (liên tục)")
            .IsRequired();

        builder.Property(entity => entity.IsDefault)
            .HasColumnName("IsDefault")
            .HasColumnType("tinyint(1)")
            .HasDefaultValue(false);

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(entity => new { entity.ProjectId, entity.Name }, "UQ_Board_Name").IsUnique();

        builder.HasIndex(entity => entity.Name, "IX_Board_Name");

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(entity => entity.ProjectId)
            .HasConstraintName("FK_Board_Project")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

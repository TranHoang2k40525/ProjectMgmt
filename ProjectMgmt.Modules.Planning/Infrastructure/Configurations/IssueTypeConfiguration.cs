using Planning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Planning.Infrastructure.Configurations;

public class IssueTypeConfiguration : IEntityTypeConfiguration<IssueType>
{
    public void Configure(EntityTypeBuilder<IssueType> builder)
    {
        builder.ToTable("IssueType", table =>
        {
            table.HasComment("Loại issue cấu hình theo từng project");
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
            .HasComment("Epic / Story / Task / Bug / Sub-task")
            .IsRequired();

        builder.Property(entity => entity.IconKey)
            .HasColumnName("IconKey")
            .HasColumnType("varchar(50)");

        builder.Property(entity => entity.ColorHex)
            .HasColumnName("ColorHex")
            .HasColumnType("char(7)")
            .HasDefaultValue("#0052CC")
            .IsRequired();

        builder.Property(entity => entity.IsSubtask)
            .HasColumnName("IsSubtask")
            .HasColumnType("tinyint(1)")
            .HasComment("Validate khi gán ParentId")
            .HasDefaultValue(false);

        builder.Property(entity => entity.HierarchyLevel)
            .HasColumnName("HierarchyLevel")
            .HasColumnType("tinyint")
            .HasComment("2=Epic, 1=Story/Task/Bug, 0=Sub-task")
            .HasDefaultValue((sbyte)1);

        builder.Property(entity => entity.OrderIndex)
            .HasColumnName("OrderIndex")
            .HasColumnType("int")
            .HasDefaultValue(0);

        builder.HasIndex(entity => new { entity.ProjectId, entity.Name }, "UQ_IssueType_Name").IsUnique();

        builder.HasIndex(entity => entity.Name, "IX_IssueType_Name");

        builder.HasIndex(entity => new { entity.ProjectId, entity.OrderIndex }, "IX_IssueType_Project_Order");

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(entity => entity.ProjectId)
            .HasConstraintName("FK_IssueType_Project")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

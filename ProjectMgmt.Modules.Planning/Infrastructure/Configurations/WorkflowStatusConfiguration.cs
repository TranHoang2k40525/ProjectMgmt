using Planning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Planning.Infrastructure.Configurations;

public class WorkflowStatusConfiguration : IEntityTypeConfiguration<WorkflowStatus>
{
    public void Configure(EntityTypeBuilder<WorkflowStatus> builder)
    {
        builder.ToTable("WorkflowStatus", table =>
        {
            table.HasComment("Mỗi Project tự định nghĩa bộ trạng thái riêng");
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
            .HasColumnType("varchar(80)")
            .IsRequired();

        builder.Property(entity => entity.Category)
            .HasColumnName("Category")
            .HasColumnType("varchar(50)")
            .HasComment("Nhãn phân loại tự do của người dùng")
            .IsRequired();

        builder.Property(entity => entity.ColorHex)
            .HasColumnName("ColorHex")
            .HasColumnType("varchar(10)")
            .HasDefaultValue("#8993A4")
            .IsRequired();

        builder.Property(entity => entity.OrderIndex)
            .HasColumnName("OrderIndex")
            .HasColumnType("int")
            .HasDefaultValue(0);

        builder.Property(entity => entity.IsInitial)
            .HasColumnName("IsInitial")
            .HasColumnType("tinyint(1)")
            .HasComment("Trạng thái mặc định khi tạo issue mới")
            .HasDefaultValue(false);

        builder.Property(entity => entity.IsDone)
            .HasColumnName("IsDone")
            .HasColumnType("tinyint(1)")
            .HasComment("Trạng thái đánh dấu hoàn thành")
            .HasDefaultValue(false);

        builder.Property(entity => entity.IsActiveWork)
            .HasColumnName("IsActiveWork")
            .HasColumnType("tinyint(1)")
            .HasComment("Trạng thái đang thực hiện")
            .HasDefaultValue(false);

        builder.Property(entity => entity.IsCanceled)
            .HasColumnName("IsCanceled")
            .HasColumnType("tinyint(1)")
            .HasComment("Trạng thái đã hủy")
            .HasDefaultValue(false);

        builder.HasIndex(entity => new { entity.ProjectId, entity.Name }, "UQ_WorkflowStatus_Name").IsUnique();
        builder.HasIndex(entity => new { entity.ProjectId, entity.OrderIndex }, "IX_WorkflowStatus_Project_Order");
        builder.HasIndex(entity => entity.Name, "IX_WorkflowStatus_Name");

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(entity => entity.ProjectId)
            .HasConstraintName("FK_WorkflowStatus_Project")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

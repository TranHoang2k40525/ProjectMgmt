using Planning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Planning.Infrastructure.Configurations;

public class WorkflowTransitionConfiguration : IEntityTypeConfiguration<WorkflowTransition>
{
    public void Configure(EntityTypeBuilder<WorkflowTransition> builder)
    {
        builder.ToTable("WorkflowTransition", table =>
        {
            table.HasComment("Luật chuyển trạng thái hợp lệ - chặn kéo thẳng ToDo -> Done");
            table.HasCheckConstraint("CK_WorkflowTransition_NotSelf", "`FromStatusId` <> `ToStatusId`");
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
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("BỔ SUNG: thiếu trong bản v2.0. Không có cột này phải JOIN 2 lần mới validate được");

        builder.Property(entity => entity.FromStatusId)
            .HasColumnName("FromStatusId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.ToStatusId)
            .HasColumnName("ToStatusId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.Name)
            .HasColumnName("Name")
            .HasColumnType("varchar(80)")
            .HasComment("Tên nút bấm hiển thị, VD \"Gửi review\"");

        builder.Property(entity => entity.RequiredPermissionCode)
            .HasColumnName("RequiredPermissionCode")
            .HasColumnType("varchar(80)")
            .HasComment("Chỉ role có quyền này mới được chuyển");

        builder.HasIndex(entity => new { entity.ProjectId, entity.FromStatusId, entity.ToStatusId }, "UQ_WorkflowTransition").IsUnique();

        builder.HasIndex(entity => entity.FromStatusId, "IX_WorkflowTransition_From");

        builder.HasIndex(entity => entity.ToStatusId, "IX_WorkflowTransition_To");

        builder.HasIndex(entity => entity.Name, "IX_WorkflowTransition_Name");

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(entity => entity.ProjectId)
            .HasConstraintName("FK_WorkflowTransition_Project")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<WorkflowStatus>()
            .WithMany()
            .HasForeignKey(entity => entity.FromStatusId)
            .HasConstraintName("FK_WorkflowTransition_From")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<WorkflowStatus>()
            .WithMany()
            .HasForeignKey(entity => entity.ToStatusId)
            .HasConstraintName("FK_WorkflowTransition_To")
            .OnDelete(DeleteBehavior.NoAction);
    }
}

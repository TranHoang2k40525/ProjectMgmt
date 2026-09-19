using Planning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Planning.Infrastructure.Configurations;

public class BoardColumnConfiguration : IEntityTypeConfiguration<BoardColumn>
{
    public void Configure(EntityTypeBuilder<BoardColumn> builder)
    {
        builder.ToTable("BoardColumn", table =>
        {
            table.HasComment("BỔ SUNG: cột board + giới hạn WIP");
            table.HasCheckConstraint("CK_BoardColumn_Wip", "`WipLimit` IS NULL OR `WipLimit` > 0");
        });

        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(entity => entity.Id).HasName("PRIMARY");

        builder.Property(entity => entity.Id)
            .HasColumnName("Id")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.BoardId)
            .HasColumnName("BoardId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.StatusId)
            .HasColumnName("StatusId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("Cột ánh xạ tới WorkflowStatus nào");

        builder.Property(entity => entity.Name)
            .HasColumnName("Name")
            .HasColumnType("varchar(80)")
            .HasComment("Ghi đè tên hiển thị, NULL = lấy theo status");

        builder.Property(entity => entity.OrderIndex)
            .HasColumnName("OrderIndex")
            .HasColumnType("int")
            .HasDefaultValue(0);

        builder.Property(entity => entity.WipLimit)
            .HasColumnName("WipLimit")
            .HasColumnType("int")
            .HasComment("BỔ SUNG: Mục 5.5 yêu cầu WIP limit nhưng v2.0 không có chỗ lưu");

        builder.HasIndex(entity => new { entity.BoardId, entity.StatusId }, "UQ_BoardColumn_Status").IsUnique();

        builder.HasIndex(entity => entity.Name, "IX_BoardColumn_Name");

        builder.HasIndex(entity => new { entity.BoardId, entity.OrderIndex }, "IX_BoardColumn_Order");

        builder.HasOne<Board>()
            .WithMany()
            .HasForeignKey(entity => entity.BoardId)
            .HasConstraintName("FK_BoardColumn_Board")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<WorkflowStatus>()
            .WithMany()
            .HasForeignKey(entity => entity.StatusId)
            .HasConstraintName("FK_BoardColumn_Status")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

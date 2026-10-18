using DeliveryIntelligence.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryIntelligence.Infrastructure.Configurations;

public class AiDatasetVersionConfiguration : IEntityTypeConfiguration<AiDatasetVersion>
{
    public void Configure(EntityTypeBuilder<AiDatasetVersion> builder)
    {
        builder.ToTable("AiDatasetVersion", table =>
        {
            table.HasComment("Version hóa dataset - so sánh model chỉ có ý nghĩa khi cùng dataset version");
        });

        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(entity => entity.Id).HasName("PRIMARY");

        builder.Property(entity => entity.Id)
            .HasColumnName("Id")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.DatasetId)
            .HasColumnName("DatasetId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.VersionTag)
            .HasColumnName("VersionTag")
            .HasColumnType("varchar(40)")
            .HasComment("v1.0.0")
            .IsRequired();

        builder.Property(entity => entity.SampleCount)
            .HasColumnName("SampleCount")
            .HasColumnType("int")
            .HasDefaultValue(0);

        builder.Property(entity => entity.TrainCount)
            .HasColumnName("TrainCount")
            .HasColumnType("int")
            .HasDefaultValue(0);

        builder.Property(entity => entity.ValidationCount)
            .HasColumnName("ValidationCount")
            .HasColumnType("int")
            .HasDefaultValue(0);

        builder.Property(entity => entity.TestCount)
            .HasColumnName("TestCount")
            .HasColumnType("int")
            .HasDefaultValue(0);

        builder.Property(entity => entity.SplitSeed)
            .HasColumnName("SplitSeed")
            .HasColumnType("int")
            .HasComment("Seed chia split - BẮT BUỘC để tái lập được kết quả");

        builder.Property(entity => entity.Checksum)
            .HasColumnName("Checksum")
            .HasColumnType("char(64)")
            .HasComment("SHA-256 của file JSONL export");

        builder.Property(entity => entity.ExportPath)
            .HasColumnName("ExportPath")
            .HasColumnType("varchar(500)");

        builder.Property(entity => entity.IsFrozen)
            .HasColumnName("IsFrozen")
            .HasColumnType("tinyint(1)")
            .HasComment("Đã đóng băng thì KHÔNG được sửa sample nữa")
            .HasDefaultValue(false);

        builder.Property(entity => entity.FrozenAt)
            .HasColumnName("FrozenAt")
            .HasColumnType("datetime(6)");

        builder.Property(entity => entity.Notes)
            .HasColumnName("Notes")
            .HasColumnType("text");

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(entity => new { entity.DatasetId, entity.VersionTag }, "UQ_AiDatasetVersion").IsUnique();

        builder.HasOne<AiDataset>()
            .WithMany()
            .HasForeignKey(entity => entity.DatasetId)
            .HasConstraintName("FK_AiDatasetVersion_Dataset")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

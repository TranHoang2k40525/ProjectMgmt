using DeliveryIntelligence.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryIntelligence.Infrastructure.Configurations;

public class AiDatasetConfiguration : IEntityTypeConfiguration<AiDataset>
{
    public void Configure(EntityTypeBuilder<AiDataset> builder)
    {
        builder.ToTable("AiDataset", table =>
        {
            table.HasComment("Bộ dữ liệu huấn luyện");
            table.HasCheckConstraint("CK_AiDataset_TaskType", "`TaskType` IN ('Breakdown','Assignment')");
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
            .HasColumnType("varchar(80)")
            .HasComment("breakdown-vi-v1, assignment-ranking-v1")
            .IsRequired();

        builder.Property(entity => entity.Name)
            .HasColumnName("Name")
            .HasColumnType("varchar(200)")
            .IsRequired();

        builder.Property(entity => entity.TaskType)
            .HasColumnName("TaskType")
            .HasColumnType("varchar(30)")
            .HasComment("Breakdown / Assignment")
            .IsRequired();

        builder.Property(entity => entity.Description)
            .HasColumnName("Description")
            .HasColumnType("text");

        builder.Property(entity => entity.Languages)
            .HasColumnName("Languages")
            .HasColumnType("json")
            .HasComment("[\"vi\",\"en\"]");

        builder.Property(entity => entity.CreatedBy)
            .HasColumnName("CreatedBy")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> User.Id");

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(entity => entity.Code, "UQ_AiDataset_Code").IsUnique();

        builder.HasIndex(entity => entity.Name, "IX_AiDataset_Name");
    }
}

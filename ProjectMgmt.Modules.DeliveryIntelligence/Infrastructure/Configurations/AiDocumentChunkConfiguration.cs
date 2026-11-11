using DeliveryIntelligence.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryIntelligence.Infrastructure.Configurations;

public class AiDocumentChunkConfiguration : IEntityTypeConfiguration<AiDocumentChunk>
{
    public void Configure(EntityTypeBuilder<AiDocumentChunk> builder)
    {
        builder.ToTable("AiDocumentChunk", table =>
        {
            table.HasComment("Đoạn tài liệu Spec Hub kèm vector embedding cho tìm kiếm ngữ nghĩa (RAG)");
            table.HasCheckConstraint("CK_AiDocumentChunk_ChunkIndex", "`ChunkIndex` >= 0");
            table.HasCheckConstraint("CK_AiDocumentChunk_Dimensions", "`Dimensions` > 0");
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
            .HasComment("XMOD -> Project.Id")
            .IsRequired();

        builder.Property(entity => entity.DocumentId)
            .HasColumnName("DocumentId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> ProjectDocument.Id")
            .IsRequired();

        builder.Property(entity => entity.ChunkIndex)
            .HasColumnName("ChunkIndex")
            .HasColumnType("int")
            .IsRequired();

        builder.Property(entity => entity.DocumentName)
            .HasColumnName("DocumentName")
            .HasColumnType("varchar(500)")
            .IsRequired();

        builder.Property(entity => entity.Heading)
            .HasColumnName("Heading")
            .HasColumnType("varchar(500)");

        builder.Property(entity => entity.PageNumber)
            .HasColumnName("PageNumber")
            .HasColumnType("int");

        builder.Property(entity => entity.Content)
            .HasColumnName("Content")
            .HasColumnType("mediumtext")
            .IsRequired();

        builder.Property(entity => entity.SourceVersion)
            .HasColumnName("SourceVersion")
            .HasColumnType("char(64)")
            .HasComment("SHA-256 của phiên bản tài liệu + model + tham số chia đoạn")
            .IsRequired();

        builder.Property(entity => entity.EmbeddingModel)
            .HasColumnName("EmbeddingModel")
            .HasColumnType("varchar(120)")
            .IsRequired();

        builder.Property(entity => entity.Dimensions)
            .HasColumnName("Dimensions")
            .HasColumnType("int")
            .IsRequired();

        builder.Property(entity => entity.Embedding)
            .HasColumnName("Embedding")
            .HasColumnType("longblob")
            .HasComment("float32 little-endian, đã chuẩn hóa độ dài 1")
            .IsRequired();

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(entity => new { entity.DocumentId, entity.ChunkIndex }, "UQ_AiDocumentChunk_Document_Chunk").IsUnique();
        builder.HasIndex(entity => new { entity.ProjectId, entity.EmbeddingModel }, "IX_AiDocumentChunk_Project_Model");
    }
}

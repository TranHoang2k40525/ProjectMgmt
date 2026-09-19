using DeliveryIntelligence.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryIntelligence.Infrastructure.Configurations;

public class AiDataCleaningRuleConfiguration : IEntityTypeConfiguration<AiDataCleaningRule>
{
    public void Configure(EntityTypeBuilder<AiDataCleaningRule> builder)
    {
        builder.ToTable("AiDataCleaningRule", table =>
        {
            table.HasComment("Rule làm sạch dạng cấu hình - không hard-code trong script Python");
            table.HasCheckConstraint("CK_AiDataCleaningRule_Severity", "`Severity` IN ('Info','Warning','Error')");
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
            .IsRequired();

        builder.Property(entity => entity.Name)
            .HasColumnName("Name")
            .HasColumnType("varchar(200)")
            .IsRequired();

        builder.Property(entity => entity.RuleType)
            .HasColumnName("RuleType")
            .HasColumnType("varchar(30)")
            .HasComment("SchemaValidation / PiiDetection / Deduplication / LengthCheck / LanguageCheck / Heuristic")
            .IsRequired();

        builder.Property(entity => entity.Severity)
            .HasColumnName("Severity")
            .HasColumnType("varchar(20)")
            .HasComment("Info / Warning / Error (Error = tự động Reject)")
            .HasDefaultValue("Warning")
            .IsRequired();

        builder.Property(entity => entity.Config)
            .HasColumnName("Config")
            .HasColumnType("json")
            .HasComment("Regex, ngưỡng min/max, danh sách từ cấm...");

        builder.Property(entity => entity.AppliesTo)
            .HasColumnName("AppliesTo")
            .HasColumnType("varchar(30)")
            .HasComment("Breakdown / Assignment / All")
            .HasDefaultValue("All")
            .IsRequired();

        builder.Property(entity => entity.IsActive)
            .HasColumnName("IsActive")
            .HasColumnType("tinyint(1)")
            .HasDefaultValue(true);

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(entity => entity.Code, "UQ_AiDataCleaningRule_Code").IsUnique();

        builder.HasIndex(entity => entity.Name, "IX_AiDataCleaningRule_Name");
    }
}

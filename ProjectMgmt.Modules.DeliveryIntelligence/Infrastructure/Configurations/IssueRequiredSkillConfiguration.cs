using DeliveryIntelligence.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryIntelligence.Infrastructure.Configurations;

public class IssueRequiredSkillConfiguration : IEntityTypeConfiguration<IssueRequiredSkill>
{
    public void Configure(EntityTypeBuilder<IssueRequiredSkill> builder)
    {
        builder.ToTable("IssueRequiredSkill", table =>
        {
            table.HasComment("BỔ SUNG - AI ASSIGNMENT: kỹ năng yêu cầu của task, ghép với UserSkill để tính skill-match");
            table.HasCheckConstraint("CK_IssueRequiredSkill_Level", "`MinLevel` BETWEEN 1 AND 5");
            table.HasCheckConstraint("CK_IssueRequiredSkill_Source", "`Source` IN ('Manual','AI','DerivedFromComponent')");
        });

        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(entity => entity.Id).HasName("PRIMARY");

        builder.Property(entity => entity.Id)
            .HasColumnName("Id")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.IssueId)
            .HasColumnName("IssueId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.SkillId)
            .HasColumnName("SkillId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> SkillCatalog.Id");

        builder.Property(entity => entity.MinLevel)
            .HasColumnName("MinLevel")
            .HasColumnType("tinyint")
            .HasComment("1..5")
            .HasDefaultValue((sbyte)1);

        builder.Property(entity => entity.Weight)
            .HasColumnName("Weight")
            .HasColumnType("decimal(4,3)")
            .HasComment("Trọng số khi tính skill-match score")
            .HasDefaultValue(1.000m);

        builder.Property(entity => entity.Source)
            .HasColumnName("Source")
            .HasColumnType("varchar(20)")
            .HasComment("Manual / AI / DerivedFromComponent")
            .HasDefaultValue("Manual")
            .IsRequired();

        builder.HasIndex(entity => new { entity.IssueId, entity.SkillId }, "UQ_IssueRequiredSkill").IsUnique();

        builder.HasIndex(entity => entity.SkillId, "IX_IssueRequiredSkill_Skill");

        builder.HasOne<Issue>()
            .WithMany()
            .HasForeignKey(entity => entity.IssueId)
            .HasConstraintName("FK_IssueRequiredSkill_Issue")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

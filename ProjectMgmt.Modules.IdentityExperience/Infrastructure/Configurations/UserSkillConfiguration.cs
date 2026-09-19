using IdentityExperience.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityExperience.Infrastructure.Configurations;

public class UserSkillConfiguration : IEntityTypeConfiguration<UserSkill>
{
    public void Configure(EntityTypeBuilder<UserSkill> builder)
    {
        builder.ToTable("UserSkill", table =>
        {
            table.HasComment("AI ASSIGNMENT: nguồn dữ liệu chính cho cold-start người mới vào project");
            table.HasCheckConstraint("CK_UserSkill_Level", "`ProficiencyLevel` BETWEEN 1 AND 5");
        });

        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(entity => entity.Id).HasName("PRIMARY");

        builder.Property(entity => entity.Id)
            .HasColumnName("Id")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.UserId)
            .HasColumnName("UserId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.SkillId)
            .HasColumnName("SkillId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.ProficiencyLevel)
            .HasColumnName("ProficiencyLevel")
            .HasColumnType("tinyint")
            .HasComment("1..5 - do user tự khai hoặc lead đánh giá")
            .HasDefaultValue((sbyte)3);

        builder.Property(entity => entity.YearsOfExperience)
            .HasColumnName("YearsOfExperience")
            .HasColumnType("decimal(4,1)");

        builder.Property(entity => entity.IsSelfDeclared)
            .HasColumnName("IsSelfDeclared")
            .HasColumnType("tinyint(1)")
            .HasComment("0 = đã được lead xác nhận, tin cậy hơn")
            .HasDefaultValue(true);

        builder.Property(entity => entity.UpdatedAt)
            .HasColumnName("UpdatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("NULL ON UPDATE CURRENT_TIMESTAMP(6)");
        builder.Property(entity => entity.UpdatedAt).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

        builder.HasIndex(entity => new { entity.UserId, entity.SkillId }, "UQ_UserSkill").IsUnique();

        builder.HasIndex(entity => new { entity.SkillId, entity.ProficiencyLevel }, "IX_UserSkill_SkillId");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .HasConstraintName("FK_UserSkill_User")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<SkillCatalog>()
            .WithMany()
            .HasForeignKey(entity => entity.SkillId)
            .HasConstraintName("FK_UserSkill_Skill")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

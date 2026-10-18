using IdentityExperience.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityExperience.Infrastructure.Configurations;

public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("UserProfile", table =>
        {
            table.HasComment("Thông tin cá nhân, tách khỏi bảng bảo mật User (1-1)");
            table.HasCheckConstraint("CK_UserProfile_Seniority", "`SeniorityLevel` IS NULL OR `SeniorityLevel` IN ('Intern','Junior','Middle','Senior','Lead','Principal')");
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

        builder.Property(entity => entity.DisplayName)
            .HasColumnName("DisplayName")
            .HasColumnType("varchar(150)")
            .IsRequired();

        builder.Property(entity => entity.AvatarUrl)
            .HasColumnName("AvatarUrl")
            .HasColumnType("varchar(500)");

        builder.Property(entity => entity.PhoneNumber)
            .HasColumnName("PhoneNumber")
            .HasColumnType("varchar(30)");

        builder.Property(entity => entity.Timezone)
            .HasColumnName("Timezone")
            .HasColumnType("varchar(64)")
            .HasDefaultValue("Asia/Ho_Chi_Minh")
            .IsRequired();

        builder.Property(entity => entity.JobTitle)
            .HasColumnName("JobTitle")
            .HasColumnType("varchar(150)")
            .HasComment("AI ASSIGNMENT: dùng cho cold-start người mới");

        builder.Property(entity => entity.SeniorityLevel)
            .HasColumnName("SeniorityLevel")
            .HasColumnType("varchar(20)")
            .HasComment("Intern/Junior/Middle/Senior/Lead - cold-start");

        builder.Property(entity => entity.YearsOfExperience)
            .HasColumnName("YearsOfExperience")
            .HasColumnType("decimal(4,1)")
            .HasComment("Cold-start khi chưa có log task");

        builder.Property(entity => entity.Bio)
            .HasColumnName("Bio")
            .HasColumnType("text");

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.Property(entity => entity.UpdatedAt)
            .HasColumnName("UpdatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("NULL ON UPDATE CURRENT_TIMESTAMP(6)");
        builder.Property(entity => entity.UpdatedAt).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

        builder.HasIndex(entity => entity.UserId, "UQ_UserProfile_UserId").IsUnique();

        builder.HasIndex(entity => entity.DisplayName, "IX_UserProfile_DisplayName");

        builder.HasIndex(entity => entity.JobTitle, "IX_UserProfile_JobTitle");

        builder.HasIndex(entity => new { entity.DisplayName, entity.JobTitle, entity.Bio }, "FT_UserProfile_Search").IsFullText();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .HasConstraintName("FK_UserProfile_User")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

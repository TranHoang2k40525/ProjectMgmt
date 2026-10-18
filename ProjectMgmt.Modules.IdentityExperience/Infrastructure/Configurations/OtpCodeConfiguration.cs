using IdentityExperience.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityExperience.Infrastructure.Configurations;

public class OtpCodeConfiguration : IEntityTypeConfiguration<OtpCode>
{
    public void Configure(EntityTypeBuilder<OtpCode> builder)
    {
        builder.ToTable("OtpCode", table =>
        {
            table.HasComment("Mã OTP đa mục đích");
            table.HasCheckConstraint("CK_OtpCode_Purpose", "`Purpose` IN ('VerifyEmail','ResetPassword','Login2FA')");
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

        builder.Property(entity => entity.CodeHash)
            .HasColumnName("CodeHash")
            .HasColumnType("varchar(255)")
            .HasComment("HASH của OTP, không lưu plain-text")
            .IsRequired();

        builder.Property(entity => entity.Purpose)
            .HasColumnName("Purpose")
            .HasColumnType("varchar(30)")
            .HasComment("VerifyEmail / ResetPassword / Login2FA")
            .IsRequired();

        builder.Property(entity => entity.ExpiresAt)
            .HasColumnName("ExpiresAt")
            .HasColumnType("datetime(6)");

        builder.Property(entity => entity.IsUsed)
            .HasColumnName("IsUsed")
            .HasColumnType("tinyint(1)")
            .HasComment("Chống replay")
            .HasDefaultValue(false);

        builder.Property(entity => entity.AttemptCount)
            .HasColumnName("AttemptCount")
            .HasColumnType("int")
            .HasComment("Chống brute-force, khóa sau N lần")
            .HasDefaultValue(0);

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(entity => new { entity.UserId, entity.Purpose, entity.IsUsed }, "IX_OtpCode_User_Purpose");

        builder.HasIndex(entity => entity.ExpiresAt, "IX_OtpCode_ExpiresAt");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .HasConstraintName("FK_OtpCode_User")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

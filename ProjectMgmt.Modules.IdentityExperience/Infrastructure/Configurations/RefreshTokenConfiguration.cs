using IdentityExperience.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityExperience.Infrastructure.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshToken", table =>
        {
            table.HasComment("Chỉ RefreshToken lưu DB; Access Token JWT tự-chứa nên không lưu");
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

        builder.Property(entity => entity.TokenHash)
            .HasColumnName("TokenHash")
            .HasColumnType("char(64)")
            .HasComment("SHA-256 hex của refresh token")
            .IsRequired();

        builder.Property(entity => entity.ExpiresAt)
            .HasColumnName("ExpiresAt")
            .HasColumnType("datetime(6)");

        builder.Property(entity => entity.IsRevoked)
            .HasColumnName("IsRevoked")
            .HasColumnType("tinyint(1)")
            .HasDefaultValue(false);

        builder.Property(entity => entity.ReplacedByTokenId)
            .HasColumnName("ReplacedByTokenId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("Chuỗi rotation - phát hiện token reuse attack");

        builder.Property(entity => entity.CreatedByIp)
            .HasColumnName("CreatedByIp")
            .HasColumnType("varchar(45)")
            .HasComment("IPv6-safe");

        builder.Property(entity => entity.UserAgent)
            .HasColumnName("UserAgent")
            .HasColumnType("varchar(400)");

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(entity => entity.TokenHash, "UQ_RefreshToken_Hash").IsUnique();

        builder.HasIndex(entity => new { entity.UserId, entity.IsRevoked }, "IX_RefreshToken_UserId");

        builder.HasIndex(entity => entity.ExpiresAt, "IX_RefreshToken_ExpiresAt");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .HasConstraintName("FK_RefreshToken_User")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<RefreshToken>()
            .WithMany()
            .HasForeignKey(entity => entity.ReplacedByTokenId)
            .HasConstraintName("FK_RefreshToken_Replaced")
            .OnDelete(DeleteBehavior.SetNull);
    }
}

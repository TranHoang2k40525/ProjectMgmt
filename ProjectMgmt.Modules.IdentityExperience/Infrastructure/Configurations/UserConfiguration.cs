using IdentityExperience.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityExperience.Infrastructure.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("User", table =>
        {
            table.HasComment("Tài khoản đăng nhập cốt lõi");
        });

        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(entity => entity.Id).HasName("PRIMARY");

        builder.Property(entity => entity.Id)
            .HasColumnName("Id")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("GUID - PK toàn hệ thống");

        builder.Property(entity => entity.Email)
            .HasColumnName("Email")
            .HasColumnType("varchar(256)")
            .HasComment("Định danh đăng nhập chính")
            .IsRequired();

        builder.Property(entity => entity.NormalizedEmail)
            .HasColumnName("NormalizedEmail")
            .HasColumnType("varchar(256)")
            .HasComment("Email viết HOA để so sánh không phân biệt hoa/thường")
            .IsRequired();

        builder.Property(entity => entity.PasswordHash)
            .HasColumnName("PasswordHash")
            .HasColumnType("varchar(255)")
            .HasComment("BCrypt/Argon2. NULL nếu tài khoản thuần OAuth");

        builder.Property(entity => entity.IsEmailVerified)
            .HasColumnName("IsEmailVerified")
            .HasColumnType("tinyint(1)")
            .HasDefaultValue(false);

        builder.Property(entity => entity.IsActive)
            .HasColumnName("IsActive")
            .HasColumnType("tinyint(1)")
            .HasComment("Soft-disable, không xóa dữ liệu")
            .HasDefaultValue(true);

        builder.Property(entity => entity.SecurityStamp)
            .HasColumnName("SecurityStamp")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("Đổi giá trị này để vô hiệu toàn bộ token cũ");

        builder.Property(entity => entity.LastLoginAt)
            .HasColumnName("LastLoginAt")
            .HasColumnType("datetime(6)");

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.Property(entity => entity.UpdatedAt)
            .HasColumnName("UpdatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("NULL ON UPDATE CURRENT_TIMESTAMP(6)");
        builder.Property(entity => entity.UpdatedAt).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

        builder.HasIndex(entity => entity.NormalizedEmail, "UQ_User_NormalizedEmail").IsUnique();

        builder.HasIndex(entity => new { entity.IsActive, entity.CreatedAt }, "IX_User_Active_Created");

        builder.HasIndex(entity => entity.LastLoginAt, "IX_User_LastLoginAt");
    }
}

using IdentityExperience.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityExperience.Infrastructure.Configurations;

public class ExternalLoginConfiguration : IEntityTypeConfiguration<ExternalLogin>
{
    public void Configure(EntityTypeBuilder<ExternalLogin> builder)
    {
        builder.ToTable("ExternalLogin", table =>
        {
            table.HasComment("Liên kết đăng nhập OAuth2");
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

        builder.Property(entity => entity.Provider)
            .HasColumnName("Provider")
            .HasColumnType("varchar(50)")
            .HasComment("Google / Microsoft / Facebook")
            .IsRequired();

        builder.Property(entity => entity.ProviderKey)
            .HasColumnName("ProviderKey")
            .HasColumnType("varchar(255)")
            .HasComment("claim \"sub\" - KHÔNG dùng email vì email đổi được")
            .IsRequired();

        builder.Property(entity => entity.LinkedAt)
            .HasColumnName("LinkedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(entity => new { entity.Provider, entity.ProviderKey }, "UQ_ExternalLogin_Provider").IsUnique();

        builder.HasIndex(entity => entity.UserId, "IX_ExternalLogin_UserId");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .HasConstraintName("FK_ExternalLogin_User")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

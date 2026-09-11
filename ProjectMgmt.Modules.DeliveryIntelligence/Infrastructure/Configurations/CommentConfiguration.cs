using DeliveryIntelligence.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryIntelligence.Infrastructure.Configurations;

public class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("Comment", table =>
        {
            table.HasComment("Bình luận do người dùng chủ động viết");
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

        builder.Property(entity => entity.UserId)
            .HasColumnName("UserId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> User.Id");

        builder.Property(entity => entity.ParentCommentId)
            .HasColumnName("ParentCommentId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("BỔ SUNG: reply lồng nhau");

        builder.Property(entity => entity.Content)
            .HasColumnName("Content")
            .HasColumnType("mediumtext")
            .HasComment("Markdown")
            .IsRequired();

        builder.Property(entity => entity.MentionedUserIds)
            .HasColumnName("MentionedUserIds")
            .HasColumnType("json")
            .HasComment("BỔ SUNG: mảng GUID được @mention, để module Notification xử lý");

        builder.Property(entity => entity.IsEdited)
            .HasColumnName("IsEdited")
            .HasColumnType("tinyint(1)")
            .HasDefaultValue(false);

        builder.Property(entity => entity.IsDeleted)
            .HasColumnName("IsDeleted")
            .HasColumnType("tinyint(1)")
            .HasDefaultValue(false);

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.Property(entity => entity.UpdatedAt)
            .HasColumnName("UpdatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("NULL ON UPDATE CURRENT_TIMESTAMP(6)");
        builder.Property(entity => entity.UpdatedAt).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

        builder.HasIndex(entity => new { entity.IssueId, entity.CreatedAt }, "IX_Comment_Issue");

        builder.HasIndex(entity => entity.ParentCommentId, "IX_Comment_Parent");

        builder.HasIndex(entity => new { entity.UserId, entity.CreatedAt }, "IX_Comment_User_Created");

        builder.HasIndex(entity => entity.Content, "FT_Comment_Content").IsFullText();

        builder.HasOne<Issue>()
            .WithMany()
            .HasForeignKey(entity => entity.IssueId)
            .HasConstraintName("FK_Comment_Issue")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Comment>()
            .WithMany()
            .HasForeignKey(entity => entity.ParentCommentId)
            .HasConstraintName("FK_Comment_Parent")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

using IdentityExperience.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityExperience.Infrastructure.Configurations;

public class AiPromptTemplateConfiguration : IEntityTypeConfiguration<AiPromptTemplate>
{
    public void Configure(EntityTypeBuilder<AiPromptTemplate> builder)
    {
        builder.ToTable("AiPromptTemplate", table =>
        {
            table.HasComment("Prompt có version - không hard-code prompt trong C#, nếu không sẽ không A/B test được");
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
            .HasComment("breakdown.system.v1")
            .IsRequired();

        builder.Property(entity => entity.Version)
            .HasColumnName("Version")
            .HasColumnType("int")
            .HasDefaultValue(1);

        builder.Property(entity => entity.TaskType)
            .HasColumnName("TaskType")
            .HasColumnType("varchar(30)")
            .IsRequired();

        builder.Property(entity => entity.Language)
            .HasColumnName("Language")
            .HasColumnType("varchar(10)")
            .HasComment("vi / en / auto")
            .HasDefaultValue("vi")
            .IsRequired();

        builder.Property(entity => entity.SystemPrompt)
            .HasColumnName("SystemPrompt")
            .HasColumnType("mediumtext")
            .IsRequired();

        builder.Property(entity => entity.UserTemplate)
            .HasColumnName("UserTemplate")
            .HasColumnType("mediumtext")
            .HasComment("Template có placeholder {{Title}}, {{Description}}, {{IssueTypes}}");

        builder.Property(entity => entity.JsonSchema)
            .HasColumnName("JsonSchema")
            .HasColumnType("json")
            .HasComment("QUAN TRỌNG: schema gửi vào tham số `format` của Ollama để ÉP định dạng ở mức decoder. Giải quyết bài toán \"AI sinh task thừa/sai format\" mà KHÔNG cần fine-tune");

        builder.Property(entity => entity.IsActive)
            .HasColumnName("IsActive")
            .HasColumnType("tinyint(1)")
            .HasDefaultValue(false);

        builder.Property(entity => entity.CreatedBy)
            .HasColumnName("CreatedBy")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> User.Id");

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(entity => new { entity.Code, entity.Version }, "UQ_AiPromptTemplate").IsUnique();

        builder.HasIndex(entity => new { entity.TaskType, entity.IsActive }, "IX_AiPromptTemplate_Active");
    }
}

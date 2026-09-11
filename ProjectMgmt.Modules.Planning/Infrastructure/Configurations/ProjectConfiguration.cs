using Planning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Planning.Infrastructure.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Project", table =>
        {
            table.HasComment("Dự án - đơn vị chứa Sprint/Issue/Board/cấu hình workflow riêng");
            table.HasCheckConstraint("CK_Project_Key_Format", "`ProjectKey` REGEXP '^[A-Z][A-Z0-9]{1,9}$'");
        });

        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(entity => entity.Id).HasName("PRIMARY");

        builder.Property(entity => entity.Id)
            .HasColumnName("Id")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.OrgId)
            .HasColumnName("OrgId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci");

        builder.Property(entity => entity.ProjectKey)
            .HasColumnName("ProjectKey")
            .HasColumnType("varchar(10)")
            .HasComment("Mã viết tắt sinh issue key, VD \"PROJ\". `Key` là từ khóa MySQL nên đổi tên cột")
            .IsRequired();

        builder.Property(entity => entity.Name)
            .HasColumnName("Name")
            .HasColumnType("varchar(200)")
            .IsRequired();

        builder.Property(entity => entity.Description)
            .HasColumnName("Description")
            .HasColumnType("text");

        builder.Property(entity => entity.LeadUserId)
            .HasColumnName("LeadUserId")
            .HasColumnType("char(36)")
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasComment("XMOD -> User.Id");

        builder.Property(entity => entity.IssueCounter)
            .HasColumnName("IssueCounter")
            .HasColumnType("int")
            .HasComment("BỔ SUNG: bộ đếm sinh IssueNumber (PROJ-1, PROJ-2...)")
            .HasDefaultValue(0);

        builder.Property(entity => entity.IsArchived)
            .HasColumnName("IsArchived")
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

        builder.HasIndex(entity => new { entity.OrgId, entity.ProjectKey }, "UQ_Project_Org_Key").IsUnique();

        builder.HasIndex(entity => entity.LeadUserId, "IX_Project_LeadUserId");

        builder.HasIndex(entity => entity.Name, "IX_Project_Name");

        builder.HasIndex(entity => new { entity.OrgId, entity.IsDeleted, entity.IsArchived, entity.Name }, "IX_Project_Org_Visible_Name");

        builder.HasIndex(entity => new { entity.Name, entity.Description }, "FT_Project_Search").IsFullText();

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(entity => entity.OrgId)
            .HasConstraintName("FK_Project_Organization")
            .OnDelete(DeleteBehavior.NoAction);
    }
}

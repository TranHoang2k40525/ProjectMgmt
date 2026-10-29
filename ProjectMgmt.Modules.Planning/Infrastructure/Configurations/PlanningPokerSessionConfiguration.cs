using Planning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Planning.Infrastructure.Configurations;

public class PlanningPokerSessionConfiguration : IEntityTypeConfiguration<PlanningPokerSession>
{
    public void Configure(EntityTypeBuilder<PlanningPokerSession> builder)
    {
        builder.ToTable("PlanningPokerSession");
        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(e => e.Id).HasName("PRIMARY");

        builder.Property(e => e.Id).HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.ProjectId).HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.SprintId).HasColumnType("char(36)");
        builder.Property(e => e.IssueId).HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.Status).HasColumnType("varchar(20)").HasDefaultValue("Voting").IsRequired();
        builder.Property(e => e.FinalStoryPoints).HasColumnType("decimal(4,1)");
        builder.Property(e => e.CreatedAt).HasColumnType("datetime(6)").HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        builder.Property(e => e.UpdatedAt).HasColumnType("datetime(6)").HasDefaultValueSql("NULL ON UPDATE CURRENT_TIMESTAMP(6)");

        builder.HasIndex(e => e.IssueId, "IX_Poker_Issue");
    }
}

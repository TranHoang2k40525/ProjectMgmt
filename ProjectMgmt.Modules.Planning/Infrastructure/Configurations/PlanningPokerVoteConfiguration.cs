using Planning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Planning.Infrastructure.Configurations;

public class PlanningPokerVoteConfiguration : IEntityTypeConfiguration<PlanningPokerVote>
{
    public void Configure(EntityTypeBuilder<PlanningPokerVote> builder)
    {
        builder.ToTable("PlanningPokerVote");
        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        builder.HasKey(e => e.Id).HasName("PRIMARY");

        builder.Property(e => e.Id).HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.SessionId).HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.UserId).HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.VoteValue).HasColumnType("decimal(4,1)").IsRequired();
        builder.Property(e => e.VotedAt).HasColumnType("datetime(6)").HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        builder.HasIndex(e => new { e.SessionId, e.UserId }, "UQ_PokerVote_User").IsUnique();

        builder.HasOne<PlanningPokerSession>()
            .WithMany()
            .HasForeignKey(e => e.SessionId)
            .HasConstraintName("FK_PokerVote_Session")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

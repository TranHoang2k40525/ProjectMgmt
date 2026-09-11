using DeliveryIntelligence.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryIntelligence.Infrastructure.Configurations;

public class AiAssignmentAccuracyConfiguration : IEntityTypeConfiguration<AiAssignmentAccuracy>
{
    public void Configure(EntityTypeBuilder<AiAssignmentAccuracy> builder)
    {
        builder.HasNoKey();
        builder.ToView("vw_AiAssignmentAccuracy");
    }
}

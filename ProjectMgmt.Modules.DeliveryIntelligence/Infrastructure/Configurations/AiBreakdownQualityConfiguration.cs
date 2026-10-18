using DeliveryIntelligence.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryIntelligence.Infrastructure.Configurations;

public class AiBreakdownQualityConfiguration : IEntityTypeConfiguration<AiBreakdownQuality>
{
    public void Configure(EntityTypeBuilder<AiBreakdownQuality> builder)
    {
        builder.HasNoKey();
        builder.ToView("vw_AiBreakdownQuality");
    }
}

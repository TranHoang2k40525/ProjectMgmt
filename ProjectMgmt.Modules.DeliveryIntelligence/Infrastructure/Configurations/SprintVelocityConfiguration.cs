using DeliveryIntelligence.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryIntelligence.Infrastructure.Configurations;

public class SprintVelocityConfiguration : IEntityTypeConfiguration<SprintVelocity>
{
    public void Configure(EntityTypeBuilder<SprintVelocity> builder)
    {
        builder.HasNoKey();
        builder.ToView("vw_SprintVelocity");
    }
}

using DeliveryIntelligence.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryIntelligence.Infrastructure.Configurations;

public class UserActiveWorkloadConfiguration : IEntityTypeConfiguration<UserActiveWorkload>
{
    public void Configure(EntityTypeBuilder<UserActiveWorkload> builder)
    {
        builder.HasNoKey();
        builder.ToView("vw_UserActiveWorkload");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal sealed class RecoveryThrottleConfiguration : IEntityTypeConfiguration<RecoveryThrottle>
{
    public void Configure(EntityTypeBuilder<RecoveryThrottle> b)
    {
        b.ToTable("RecoveryThrottles");
        b.HasKey(x => x.Id);
        b.Property(x => x.KeyHash).HasMaxLength(64);
        b.HasIndex(x => x.KeyHash).IsUnique();
    }
}

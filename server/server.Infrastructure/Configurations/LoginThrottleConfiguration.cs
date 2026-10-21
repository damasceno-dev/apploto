using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal sealed class LoginThrottleConfiguration : IEntityTypeConfiguration<LoginThrottle>
{
    public void Configure(EntityTypeBuilder<LoginThrottle> b)
    {
        b.ToTable("LoginThrottles", t => t.HasCheckConstraint("CK_LoginThrottles_FailureCount_Positive", "\"FailureCount\" > 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.KeyHash).HasMaxLength(64);
        b.Property(x => x.AccountKeyHash).HasMaxLength(64);
        b.HasIndex(x => x.KeyHash).IsUnique();
        b.HasIndex(x => x.AccountKeyHash);
        b.HasIndex(x => x.ExpiresAt);
    }
}

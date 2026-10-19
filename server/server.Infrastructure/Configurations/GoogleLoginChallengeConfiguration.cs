using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal sealed class GoogleLoginChallengeConfiguration : IEntityTypeConfiguration<GoogleLoginChallenge>
{
    public void Configure(EntityTypeBuilder<GoogleLoginChallenge> b)
    {
        b.ToTable("GoogleLoginChallenges");
        b.HasKey(x => x.Id);
        b.Property(x => x.StateHash).HasMaxLength(64);
        b.Property(x => x.NonceHash).HasMaxLength(64);
        b.HasIndex(x => x.StateHash).IsUnique();
        b.HasIndex(x => x.NonceHash).IsUnique();
    }
}

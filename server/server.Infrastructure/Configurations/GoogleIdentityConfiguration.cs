using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal sealed class GoogleIdentityConfiguration : IEntityTypeConfiguration<GoogleIdentity>
{
    public void Configure(EntityTypeBuilder<GoogleIdentity> b)
    {
        b.ToTable("GoogleIdentities");
        b.HasKey(x => x.Id);
        b.Property(x => x.Issuer).HasMaxLength(100);
        b.Property(x => x.Subject).HasMaxLength(255);
        b.HasIndex(x => new { x.Issuer, x.Subject }).IsUnique();
        b.HasIndex(x => x.UserId).IsUnique();
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

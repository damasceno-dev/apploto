using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal sealed class OperatedOnboardingConfiguration : IEntityTypeConfiguration<OperatedOnboarding>
{
    public void Configure(EntityTypeBuilder<OperatedOnboarding> b)
    {
        b.ToTable("OperatedOnboardings");
        b.HasKey(x => x.Id);
        b.Property(x => x.RequestHash).HasMaxLength(64);
        b.HasIndex(x => x.RequestId).IsUnique();
        b.HasIndex(x => x.BranchId).IsUnique();
        b.HasIndex(x => x.OwnerInvitationId).IsUnique();
        b.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.OwnerInvitation).WithMany().HasForeignKey(x => x.OwnerInvitationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.PlatformUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

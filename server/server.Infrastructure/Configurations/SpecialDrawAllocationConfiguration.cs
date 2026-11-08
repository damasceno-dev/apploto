using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;
namespace server.Infrastructure.Configurations;

internal class SpecialDrawAllocationConfiguration : IEntityTypeConfiguration<SpecialDrawAllocation>
{
    public void Configure(EntityTypeBuilder<SpecialDrawAllocation> b)
    {
        b.ToTable("SpecialDrawAllocations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Version).IsRowVersion();
        b.Property(x => x.PostedOn).HasColumnType("date");
        b.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        b.Property(x => x.Evidence).HasMaxLength(1000).IsRequired();
        b.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.BranchId, x.PostedOn });
        b.HasOne<SpecialDrawAllocation>().WithMany().HasForeignKey(x => x.PreviousId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.PreviousId).IsUnique().HasFilter("\"PreviousId\" IS NOT NULL");
        b.Property(x => x.SourceValue).HasPrecision(14, 2).IsRequired();
        b.Property(x => x.SourceDate).HasColumnType("date");
        b.Property(x => x.Amount).HasPrecision(14, 2).IsRequired();
        b.Property(x => x.SignedContribution).HasPrecision(14, 2).IsRequired();
        b.HasOne<SpecialDraw>().WithMany().HasForeignKey(x => x.DrawId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DailyClose>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SessionFieldEffect>().WithMany().HasForeignKey(x => x.SessionFieldEffectId).OnDelete(DeleteBehavior.Restrict);
    }
}

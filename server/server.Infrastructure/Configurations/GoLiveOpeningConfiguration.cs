using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class GoLiveOpeningConfiguration : IEntityTypeConfiguration<GoLiveOpening>
{
    public void Configure(EntityTypeBuilder<GoLiveOpening> b)
    {
        b.ToTable("GoLiveOpenings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Version).IsRowVersion();
        b.HasOne<Transaction>().WithMany().HasForeignKey(x => x.ClientCreditTransactionId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.SourceReference).HasMaxLength(100);
        b.HasOne<GoLiveOpening>().WithMany().HasForeignKey(x => x.PreviousId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Identity).HasMaxLength(200);
        b.Property(x => x.SourceEvidence).HasMaxLength(1000);
        b.Property(x => x.CancellationReason).HasMaxLength(500);
        b.Property(x => x.AsOfDate).HasColumnType("date");
        b.HasIndex(x => new { x.BranchId, x.Identity }).IsUnique().HasFilter("\"Active\"");
        b.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

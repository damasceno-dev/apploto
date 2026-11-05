using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class BankMovementConfiguration : IEntityTypeConfiguration<BankMovement>
{
    public void Configure(EntityTypeBuilder<BankMovement> b)
    {
        b.ToTable("BankMovements"); b.HasKey(e => e.Id); b.Property(e => e.Kind).HasConversion<short>();
        b.Property(e => e.Evidence).HasMaxLength(1000);
        b.HasOne(e => e.BankReceipt).WithMany().HasForeignKey(e => e.BankReceiptId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.BankTransaction).WithMany().HasForeignKey(e => e.BankTransactionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.SourceTransaction).WithMany().HasForeignKey(e => e.SourceTransactionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => e.BankReceiptId).IsUnique(); b.HasIndex(e => e.BankTransactionId).IsUnique();
        b.HasIndex(e => e.SourceTransactionId).IsUnique().HasFilter("\"SourceTransactionId\" IS NOT NULL");
    }
}

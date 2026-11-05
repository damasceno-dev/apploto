using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class CardProviderConfiguration : IEntityTypeConfiguration<CardProvider>
{
    public void Configure(EntityTypeBuilder<CardProvider> b)
    {
        b.ToTable("CardProviders");
        b.HasKey(p => p.Id);
        b.Property(p => p.Version).IsRowVersion();
        b.Property(p => p.Name).HasMaxLength(100);
        b.Property(p => p.ExternalIdentity).HasMaxLength(100);
        b.Property(p => p.Brand).HasMaxLength(50);
        b.Property(p => p.Instrument).HasConversion<short>();
        b.HasOne<Branch>().WithMany().HasForeignKey(p => p.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Account>().WithMany().HasForeignKey(p => p.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(p => p.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(p => new { p.BranchId, p.ExternalIdentity }).IsUnique();
        b.ToTable(t => t.HasCheckConstraint("CK_CardProviders_Instrument", "\"Instrument\" IN (2, 3, 4, 5)"));
        b.ToTable(t => t.HasCheckConstraint("CK_CardProviders_Delay", "\"DelayDays\" BETWEEN 0 AND 366"));
    }
}

internal class CardSettlementBatchConfiguration : IEntityTypeConfiguration<CardSettlementBatch>
{
    public void Configure(EntityTypeBuilder<CardSettlementBatch> b)
    {
        b.ToTable("CardSettlementBatches");
        b.HasKey(p => p.Id);
        b.Property(p => p.ExternalIdentity).HasMaxLength(100);
        b.Property(p => p.ProviderName).HasMaxLength(100);
        b.Property(p => p.Brand).HasMaxLength(50);
        b.Property(p => p.FeeBasis).HasMaxLength(500);
        b.Property(p => p.FeeEvidence).HasMaxLength(1000);
        b.Property(p => p.GrossAmount).HasColumnType("numeric(14,2)");
        b.Property(p => p.ConfirmedFee).HasColumnType("numeric(14,2)");
        b.Property(p => p.ObservedNetAmount).HasColumnType("numeric(14,2)");
        b.Property(p => p.PlannedArrivalOn).HasColumnType("date");
        b.Property(p => p.FeeFinancialClass).HasConversion<short>();
        b.HasOne(p => p.Provider).WithMany().HasForeignKey(p => p.ProviderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p => p.BankReceipt).WithMany().HasForeignKey(p => p.BankReceiptId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Branch>().WithMany().HasForeignKey(p => p.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(p => p.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(p => new { p.ProviderId, p.ExternalIdentity }).IsUnique();
        b.HasIndex(p => p.BankReceiptId).IsUnique();
        b.ToTable(t => t.HasCheckConstraint("CK_CardSettlementBatches_FeeEvidence",
            "\"FeeFinancialClass\" = 3 AND ((\"ConfirmedFee\" IS NULL AND NOT \"ManagerConfirmedFee\" AND \"FeeBasis\" IS NULL AND \"FeeEvidence\" IS NULL) OR " +
            "(\"ConfirmedFee\" IS NOT NULL AND \"ManagerConfirmedFee\" AND length(btrim(\"FeeBasis\")) > 0 AND length(btrim(\"FeeEvidence\")) > 0 AND \"FeeBasis\" IS NOT NULL AND \"FeeEvidence\" IS NOT NULL))"));
        b.ToTable(t => t.HasCheckConstraint("CK_CardSettlementBatches_Amounts", "\"GrossAmount\" > 0 AND (\"ConfirmedFee\" IS NULL OR (\"ConfirmedFee\" >= 0 AND \"ConfirmedFee\" < \"GrossAmount\")) AND (\"ObservedNetAmount\" IS NULL OR \"ObservedNetAmount\" > 0)"));
    }
}

internal class CardSettlementFeeEvidenceConfiguration : IEntityTypeConfiguration<CardSettlementFeeEvidence>
{
    public void Configure(EntityTypeBuilder<CardSettlementFeeEvidence> b)
    {
        b.ToTable("CardSettlementFeeEvidence"); b.HasKey(e => e.Id);
        b.HasOne(e => e.Batch).WithMany().HasForeignKey(e => e.BatchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => new { e.BatchId, e.Sequence }).IsUnique();
        b.Property(e => e.ConfirmedFee).HasColumnType("numeric(14,2)");
        b.Property(e => e.ObservedNetAmount).HasColumnType("numeric(14,2)");
        b.Property(e => e.OccurredOn).HasColumnType("date"); b.Property(e => e.PostedOn).HasColumnType("date");
        b.Property(e => e.Basis).HasMaxLength(500); b.Property(e => e.Evidence).HasMaxLength(1000); b.Property(e => e.Reason).HasMaxLength(500);
        b.Property(e => e.FeeFinancialClass).HasConversion<short>();
        b.ToTable(t => t.HasCheckConstraint("CK_CardSettlementFeeEvidence_Values", "\"Sequence\" > 0 AND \"ConfirmedFee\" >= 0 AND (\"ObservedNetAmount\" IS NULL OR \"ObservedNetAmount\" > 0) AND \"FeeFinancialClass\" = 3 AND \"PostedOn\" >= \"OccurredOn\" AND length(btrim(\"Basis\")) > 0 AND length(btrim(\"Evidence\")) > 0 AND length(btrim(\"Reason\")) > 0"));
    }
}

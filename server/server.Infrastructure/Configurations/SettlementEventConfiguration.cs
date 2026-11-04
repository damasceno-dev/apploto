using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class SettlementEventConfiguration : IEntityTypeConfiguration<SettlementEvent>
{
    public const int ReturnReasonMaxLength = 100;

    public void Configure(EntityTypeBuilder<SettlementEvent> builder)
    {
        builder.ToTable("SettlementEvents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Kind).HasConversion<short>().IsRequired();
        builder.Property(e => e.Provenance).HasConversion<short>().IsRequired();
        builder.Property(e => e.MigrationIssue).HasConversion<short>().IsRequired();
        builder.Property(e => e.SettlementInstrument).HasConversion<short>().IsRequired();
        builder.Property(e => e.Amount).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(e => e.OutstandingDelta).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(e => e.OccurredOn).HasColumnType("date").IsRequired();
        builder.Property(e => e.PostedOn).HasColumnType("date").IsRequired();
        builder.Property(e => e.NextExpectedDepositDate).HasColumnType("date").IsRequired(false);
        builder.Property(e => e.SourcePaidAt).HasColumnType("timestamp with time zone").IsRequired(false);
        builder.Property(e => e.ReturnReason).HasMaxLength(ReturnReasonMaxLength).IsRequired(false);

        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ReceivableTransaction)
            .WithMany()
            .HasForeignKey(e => e.ReceivableTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SettlementEvent>().WithMany().HasForeignKey(e => e.PreviousEventId).IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SettlementEvent>().WithMany().HasForeignKey(e => e.ReversesEventId).IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.BankReceipt)
            .WithMany(receipt => receipt.Allocations)
            .HasForeignKey(e => e.BankReceiptId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(e => e.CustodyFromAccountId).IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(e => e.CustodyToAccountId).IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Client>().WithMany().HasForeignKey(e => e.ClientId).IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Transaction>().WithMany().HasForeignKey(e => e.ConvertedTransactionId).IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.CreatedByUserId).IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // One chain per receivable: a sequence is taken once, a predecessor has one successor, a deposit is reversed
        // once, a conversion opens one debt, and the upgrade reconstructs at most one event per receivable.
        builder.HasIndex(e => new { e.ReceivableTransactionId, e.Sequence }).IsUnique();
        builder.HasIndex(e => e.PreviousEventId).IsUnique().HasFilter("\"PreviousEventId\" IS NOT NULL");
        builder.HasIndex(e => e.ReversesEventId).IsUnique().HasFilter("\"ReversesEventId\" IS NOT NULL");
        builder.HasIndex(e => e.ConvertedTransactionId).IsUnique().HasFilter("\"ConvertedTransactionId\" IS NOT NULL");
        builder.HasIndex(e => e.ReceivableTransactionId)
            .IsUnique()
            .HasDatabaseName("IX_SettlementEvents_ReceivableTransactionId_Migrated")
            .HasFilter("\"Provenance\" = 2");
        builder.HasIndex(e => new { e.BranchId, e.PostedOn });
        builder.HasIndex(e => e.BankReceiptId).HasFilter("\"BankReceiptId\" IS NOT NULL");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_SettlementEvents_AmountPositive", "\"Amount\" > 0");
            table.HasCheckConstraint(
                "CK_SettlementEvents_Chain",
                "\"Sequence\" >= 1 AND ((\"Sequence\" = 1) = (\"PreviousEventId\" IS NULL))");
            table.HasCheckConstraint("CK_SettlementEvents_PostedOnOrAfterOccurred", "\"PostedOn\" >= \"OccurredOn\"");
            table.HasCheckConstraint(
                "CK_SettlementEvents_OutstandingDelta",
                "CASE \"Kind\" " +
                "WHEN 1 THEN \"OutstandingDelta\" = -\"Amount\" OR (\"OutstandingDelta\" = 0 AND (\"MigrationIssue\" & 1) = 1) " +
                "WHEN 3 THEN \"OutstandingDelta\" = -\"Amount\" " +
                "WHEN 6 THEN \"OutstandingDelta\" = -\"Amount\" " +
                "WHEN 4 THEN \"OutstandingDelta\" = \"Amount\" " +
                "WHEN 2 THEN \"OutstandingDelta\" = 0 " +
                "WHEN 5 THEN \"OutstandingDelta\" = 0 " +
                "ELSE false END");
            table.HasCheckConstraint(
                "CK_SettlementEvents_Provenance",
                "(\"Provenance\" = 1 AND \"Kind\" <> 1 AND \"CreatedByUserId\" IS NOT NULL AND \"SourcePaidAt\" IS NULL " +
                "AND \"MigrationIssue\" = 0) OR " +
                "(\"Provenance\" = 2 AND \"Kind\" = 1 AND \"Sequence\" = 1 AND \"CreatedByUserId\" IS NULL " +
                "AND \"SourcePaidAt\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_SettlementEvents_KindDetails",
                "(\"Kind\" = 3) = (\"BankReceiptId\" IS NOT NULL) AND " +
                "(\"Kind\" = 4) = (\"ReversesEventId\" IS NOT NULL) AND " +
                "(\"Kind\" = 6) = (\"ConvertedTransactionId\" IS NOT NULL) AND " +
                "(\"Kind\" = 2) = (\"CustodyToAccountId\" IS NOT NULL) AND " +
                "(\"Kind\" <> 4 OR (\"ClientId\" IS NOT NULL AND \"NextExpectedDepositDate\" IS NOT NULL)) AND " +
                "(\"Kind\" <> 6 OR \"ClientId\" IS NOT NULL) AND " +
                "(\"Kind\" = 2 OR \"CustodyFromAccountId\" IS NULL) AND " +
                "(\"ReturnReason\" IS NULL OR \"Kind\" = 4)");
        });
    }
}

internal class BankReceiptConfiguration : IEntityTypeConfiguration<BankReceipt>
{
    public void Configure(EntityTypeBuilder<BankReceipt> builder)
    {
        builder.ToTable("BankReceipts");
        builder.HasKey(receipt => receipt.Id);

        builder.Property(receipt => receipt.SourceKind).HasConversion<short>().IsRequired();
        builder.Property(receipt => receipt.Status).HasConversion<short>().IsRequired();
        builder.Property(receipt => receipt.Amount).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(receipt => receipt.OccurredOn).HasColumnType("date").IsRequired();
        builder.Property(receipt => receipt.PostedOn).HasColumnType("date").IsRequired();
        builder.Property(receipt => receipt.Description).HasMaxLength(500).IsRequired(false);

        builder.HasOne<Branch>().WithMany().HasForeignKey(receipt => receipt.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(receipt => receipt.BankAccount)
            .WithMany()
            .HasForeignKey(receipt => receipt.BankAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(receipt => receipt.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(receipt => new { receipt.BranchId, receipt.BankAccountId, receipt.Status });

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_BankReceipts_AmountPositive", "\"Amount\" > 0");
            table.HasCheckConstraint("CK_BankReceipts_PostedOnOrAfterOccurred", "\"PostedOn\" >= \"OccurredOn\"");
        });
    }
}

internal class TransactionClassificationRevisionConfiguration : IEntityTypeConfiguration<TransactionClassificationRevision>
{
    public const int ReasonMaxLength = 500;

    public void Configure(EntityTypeBuilder<TransactionClassificationRevision> builder)
    {
        builder.ToTable("TransactionClassificationRevisions");
        builder.HasKey(revision => revision.Id);

        builder.Property(revision => revision.PreviousSettlementInstrument).HasConversion<short?>().IsRequired(false);
        builder.Property(revision => revision.PreviousMonthlyFinancialRole).HasConversion<short?>().IsRequired(false);
        builder.Property(revision => revision.PreviousFinancialClass).HasConversion<short?>().IsRequired(false);
        builder.Property(revision => revision.PreviousProvenance).HasConversion<short?>().IsRequired(false);
        builder.Property(revision => revision.SettlementInstrument).HasConversion<short>().IsRequired();
        builder.Property(revision => revision.MonthlyFinancialRole).HasConversion<short>().IsRequired();
        builder.Property(revision => revision.FinancialClass).HasConversion<short>().IsRequired();
        builder.Property(revision => revision.Reason).HasMaxLength(ReasonMaxLength).IsRequired();

        builder.HasOne<Branch>().WithMany().HasForeignKey(revision => revision.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Transaction>().WithMany().HasForeignKey(revision => revision.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(revision => revision.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(revision => new { revision.TransactionId, revision.Revision }).IsUnique();
        builder.HasIndex(revision => revision.BatchId);

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_TransactionClassificationRevisions_RevisionAfterPosting", "\"Revision\" >= 2"));
    }
}

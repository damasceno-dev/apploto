using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Version).IsRowVersion();
        builder.Property(t => t.Date).HasColumnType("date").IsRequired();
        builder.Property(t => t.Value).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(t => t.Description).HasMaxLength(500).IsRequired(false);
        builder.Property(t => t.TransactionTime).HasColumnType("time without time zone").IsRequired(false);
        builder.Property(t => t.Direction).HasConversion<short>().IsRequired();
        builder.Property(t => t.DueDate).HasColumnType("date").IsRequired();
        builder.Property(t => t.UpdatedAt).HasColumnType("timestamp with time zone").IsRequired(false);
        builder.Property(t => t.UpdatedByUserId).IsRequired(false);
        builder.Property(t => t.Status).HasConversion<short>().IsRequired();
        builder.Property(t => t.CancelledAt).HasColumnType("timestamp with time zone").IsRequired(false);
        builder.Property(t => t.CancellationReason).HasMaxLength(500).IsRequired(false);

        builder.HasOne(t => t.Account)
            .WithMany()
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.TransactionType)
            .WithMany()
            .HasForeignKey(t => t.TransactionTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Category)
            .WithMany()
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Client)
            .WithMany()
            .HasForeignKey(t => t.ClientId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.RecordedByOperator)
            .WithMany()
            .HasForeignKey(t => t.RecordedByOperatorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.CreatedByUser)
            .WithMany()
            .HasForeignKey(t => t.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.CancelledByUser)
            .WithMany()
            .HasForeignKey(t => t.CancelledByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.OriginTransaction)
            .WithMany()
            .HasForeignKey(t => t.OriginTransactionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // The composite (BranchId, AccountId, SessionId) foreign key to the session's alternate key is installed by the
        // M9.6 Phase 2 migration in SQL, so a row can never attach to another cashbox's session.
        builder.HasOne(t => t.Session)
            .WithMany()
            .HasForeignKey(t => t.SessionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => new { t.SessionId, t.Status })
            .HasFilter("\"SessionId\" IS NOT NULL");

        builder.HasOne(t => t.Branch)
            .WithMany()
            .HasForeignKey(t => t.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => new { t.BranchId, t.Date, t.AccountId });
        builder.HasIndex(t => new { t.BranchId, t.AccountId, t.Direction, t.Date });

        builder.HasIndex(t => new { t.BranchId, t.DueDate });

        // Frozen classification (M9.7 Phase 1.3): the database captures it on posting (TR_Transactions_Classification)
        // and changes it only with an audited revision, so the application never sends these columns; it reads back
        // the stored values after every insert and update.
        ConfigureDatabaseOwned(builder.Property(t => t.PostedSettlementInstrument).HasConversion<short?>());
        ConfigureDatabaseOwned(builder.Property(t => t.PostedMonthlyFinancialRole).HasConversion<short?>());
        ConfigureDatabaseOwned(builder.Property(t => t.PostedFinancialClass).HasConversion<short?>());
        ConfigureDatabaseOwned(builder.Property(t => t.ClassificationProvenance).HasConversion<short?>());
        ConfigureDatabaseOwned(builder.Property(t => t.ClassificationRevision).HasDefaultValue(0));
        ConfigureDatabaseOwned(builder.Property(t => t.ClassifiedAt).HasColumnType("timestamp with time zone"));
        builder.HasIndex(t => new { t.BranchId, t.PostedSettlementInstrument, t.DueDate })
            .HasFilter("\"Status\" = 1 AND \"PostedSettlementInstrument\" <> 0");
        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_Transactions_ClassificationSnapshot",
                "(\"ClassificationRevision\" = 0 AND \"ClassificationProvenance\" IS NULL AND \"PostedSettlementInstrument\" IS NULL " +
                "AND \"PostedMonthlyFinancialRole\" IS NULL AND \"PostedFinancialClass\" IS NULL AND \"ClassifiedAt\" IS NULL) OR " +
                "(\"ClassificationRevision\" >= 1 AND \"ClassificationProvenance\" IS NOT NULL AND \"PostedSettlementInstrument\" IS NOT NULL " +
                "AND \"PostedMonthlyFinancialRole\" IS NOT NULL AND \"PostedFinancialClass\" IS NOT NULL AND \"ClassifiedAt\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_Transactions_ActiveIsClassified",
                "\"Status\" <> 1 OR \"ClassificationRevision\" >= 1");
        });

        builder.HasIndex(t => t.OriginTransactionId)
            .HasFilter("\"OriginTransactionId\" IS NOT NULL");

        builder.Property(t => t.GenerationKind).HasConversion<short?>().IsRequired(false);
        builder.HasOne(t => t.GeneratedFromTransaction)
            .WithMany()
            .HasForeignKey(t => t.GeneratedFromTransactionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // One generated counterpart per source. Fiado, cofre and cheque-conversion rows (kinds 1, 2, 6, 7) hang off their
        // source row; a
        // close-field effect (kinds 3–5) has none and is owned by its session's field slot instead (M9.6 Phase 7).
        builder.HasIndex(t => t.GeneratedFromTransactionId)
            .IsUnique()
            .HasFilter("\"GeneratedFromTransactionId\" IS NOT NULL");
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Transactions_GenerationOrigin",
            "(\"GenerationKind\" IS NULL AND \"GeneratedFromTransactionId\" IS NULL) OR " +
            "(\"GenerationKind\" IN (1, 2, 6, 7) AND \"GeneratedFromTransactionId\" IS NOT NULL) OR " +
            "(\"GenerationKind\" IN (3, 4, 5) AND \"GeneratedFromTransactionId\" IS NULL AND \"SessionId\" IS NOT NULL)"));

        // At most one live (non-cancelled) effect per session and close field, whatever the save path.
        builder.HasIndex(t => new { t.SessionId, t.GenerationKind })
            .IsUnique()
            .HasDatabaseName("IX_Transactions_SessionId_GenerationKind_LiveFieldEffect")
            .HasFilter("\"GenerationKind\" IN (3, 4, 5) AND \"Status\" <> 2");

        builder.HasIndex(t => new { t.BranchId, t.Status });
        builder.HasIndex(t => new { t.BranchId, t.RecordedByOperatorId, t.Date });
    }

    private static void ConfigureDatabaseOwned<TProperty>(PropertyBuilder<TProperty> property)
    {
        property.IsRequired(typeof(TProperty) == typeof(int)).ValueGeneratedOnAddOrUpdate();
        property.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
        property.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
    }
}

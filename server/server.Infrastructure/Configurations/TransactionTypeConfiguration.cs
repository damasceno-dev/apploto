using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class TransactionTypeConfiguration : IEntityTypeConfiguration<TransactionType>
{
    public void Configure(EntityTypeBuilder<TransactionType> builder)
    {
        builder.ToTable("TransactionTypes");
        builder.HasKey(transactionType => transactionType.Id);

        builder.Property(transactionType => transactionType.Name).HasMaxLength(255);
        builder.Property(transactionType => transactionType.SettlementRule).HasConversion<short>().IsRequired();
        builder.Property(transactionType => transactionType.RequiresTabAccountAndClient).IsRequired();
        builder.Property(transactionType => transactionType.SystemRole).HasConversion<short?>().IsRequired(false);
        builder.Property(transactionType => transactionType.FiadoPaymentKind).HasConversion<short?>().IsRequired(false);
        builder.Property(transactionType => transactionType.SettlementInstrument).HasConversion<short>().IsRequired();
        builder.Property(transactionType => transactionType.MonthlyFinancialRole).HasConversion<short>().IsRequired();

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_TransactionTypes_SystemRole_NotPaymentMethod",
            "\"SystemRole\" IS NULL OR \"FiadoPaymentKind\" IS NULL"));
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_TransactionTypes_SystemRole_NoSettlementInstrument",
            "\"SystemRole\" IS NULL OR \"SettlementInstrument\" = 0"));
        // A system type's monthly role is fixed by its system role: the two fee roles carry their fee, every other
        // system type none (M9.6 Phase 7).
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_TransactionTypes_SystemRole_NoMonthlyFinancialRole",
            "\"SystemRole\" IS NULL OR \"MonthlyFinancialRole\" = CASE \"SystemRole\" WHEN 5 THEN 1 WHEN 6 THEN 3 ELSE 0 END"));
        builder.Property(transactionType => transactionType.SeedKey).HasMaxLength(64).IsRequired(false);

        // Seeding finds its own rows by key inside the category it seeds; a custom type never carries one.
        builder.HasIndex(transactionType => new { transactionType.CategoryId, transactionType.SeedKey })
            .IsUnique()
            .HasDatabaseName("IX_TransactionTypes_CategoryId_SeedKey")
            .HasFilter("\"SeedKey\" IS NOT NULL");

        builder.HasIndex(transactionType => new { transactionType.CategoryId, transactionType.Name })
            .IsUnique()
            .HasDatabaseName("IX_TransactionTypes_CategoryId_Name")
            .HasFilter("\"Active\" = true");
    }
}

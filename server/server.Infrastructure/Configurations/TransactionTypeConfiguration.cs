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
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_TransactionTypes_SystemRole_NoMonthlyFinancialRole",
            "\"SystemRole\" IS NULL OR \"MonthlyFinancialRole\" = 0"));

        builder.HasIndex(transactionType => new { transactionType.CategoryId, transactionType.Name })
            .IsUnique()
            .HasDatabaseName("IX_TransactionTypes_CategoryId_Name")
            .HasFilter("\"Active\" = true");
    }
}

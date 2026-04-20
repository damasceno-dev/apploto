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

        builder.HasIndex(transactionType => new { transactionType.CategoryId, transactionType.Name }).IsUnique();
    }
}

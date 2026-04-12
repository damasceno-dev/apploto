using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class OperatorAccountConfiguration : IEntityTypeConfiguration<OperatorAccount>
{
    public void Configure(EntityTypeBuilder<OperatorAccount> builder)
    {
        builder.ToTable("OperatorAccounts");
        builder.HasKey(oa => oa.Id);

        builder.HasOne(oa => oa.Operator)
            .WithMany(op => op.OperatorAccounts)
            .HasForeignKey(oa => oa.OperatorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(oa => oa.Account)
            .WithMany(a => a.OperatorAccounts)
            .HasForeignKey(oa => oa.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // Hard uniqueness on (OperatorId, AccountId)
        builder.HasIndex(oa => new { oa.OperatorId, oa.AccountId }).IsUnique();

        // At most one active primary account per operator
        builder.HasIndex(oa => oa.OperatorId)
            .IsUnique()
            .HasFilter("\"IsPrimary\" = true AND \"Active\" = true");
    }
}

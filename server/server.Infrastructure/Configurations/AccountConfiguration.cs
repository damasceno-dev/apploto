using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Type).IsRequired();
        builder.Property(a => a.Name).HasMaxLength(255).IsRequired();
        builder.Property(a => a.Institution).HasMaxLength(255).IsRequired(false);
        builder.Property(a => a.Number).HasMaxLength(100).IsRequired(false);

        builder.HasOne(a => a.Branch)
            .WithMany()
            .HasForeignKey(a => a.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.TabAccount)
            .WithMany()
            .HasForeignKey(a => a.TabAccountId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // A Tab account can belong to at most one Terminal
        builder.HasIndex(a => a.TabAccountId)
            .IsUnique()
            .HasFilter("\"TabAccountId\" IS NOT NULL");
    }
}

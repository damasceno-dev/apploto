using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Date).HasColumnType("date").IsRequired();
        builder.Property(t => t.Value).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(t => t.Description).HasMaxLength(500).IsRequired(false);
        builder.Property(t => t.TransactionTime).HasColumnType("time without time zone").IsRequired(false);
        builder.Property(t => t.Direction).HasConversion<short>().IsRequired();
        builder.Property(t => t.DueDate).HasColumnType("date").IsRequired();
        builder.Property(t => t.PaidAt).HasColumnType("timestamp with time zone").IsRequired(false);
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

        builder.HasOne(t => t.Branch)
            .WithMany()
            .HasForeignKey(t => t.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => new { t.BranchId, t.Date, t.AccountId });
        builder.HasIndex(t => new { t.BranchId, t.AccountId, t.Direction, t.Date });

        builder.HasIndex(t => new { t.BranchId, t.DueDate })
            .HasFilter("\"PaidAt\" IS NULL");

        builder.HasIndex(t => t.OriginTransactionId)
            .HasFilter("\"OriginTransactionId\" IS NOT NULL");

        builder.HasIndex(t => new { t.BranchId, t.Status });
        builder.HasIndex(t => new { t.BranchId, t.RecordedByOperatorId, t.Date });
    }
}

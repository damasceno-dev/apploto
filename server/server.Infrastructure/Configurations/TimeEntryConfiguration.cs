using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class TimeEntryConfiguration : IEntityTypeConfiguration<TimeEntry>
{
    public void Configure(EntityTypeBuilder<TimeEntry> builder)
    {
        builder.ToTable("TimeEntries");
        builder.HasKey(te => te.Id);

        builder.Property(te => te.Date).HasColumnType("date").IsRequired();
        builder.Property(te => te.ClockIn).HasColumnType("time without time zone").IsRequired(false);
        builder.Property(te => te.ClockOut).HasColumnType("time without time zone").IsRequired(false);
        builder.Property(te => te.Status).HasConversion<short>().IsRequired();
        builder.Property(te => te.TotalHours).HasPrecision(6, 2).IsRequired();
        builder.Property(te => te.BalanceHours).HasPrecision(6, 2).IsRequired();
        builder.Property(te => te.UpdatedAt).HasColumnType("timestamp with time zone").IsRequired(false);
        builder.Property(te => te.UpdatedByUserId).IsRequired(false);

        builder.HasOne(te => te.Operator)
            .WithMany()
            .HasForeignKey(te => te.OperatorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(te => te.Branch)
            .WithMany()
            .HasForeignKey(te => te.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(te => te.UpdatedByUser)
            .WithMany()
            .HasForeignKey(te => te.UpdatedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(te => new { te.BranchId, te.OperatorId, te.Date })
            .IsUnique()
            .HasDatabaseName("IX_TimeEntries_BranchId_OperatorId_Date")
            .HasFilter("\"Active\" = true");

        builder.HasIndex(te => new { te.BranchId, te.Date });
    }
}

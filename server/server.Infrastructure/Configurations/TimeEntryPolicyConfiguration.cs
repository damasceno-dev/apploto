using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class TimeEntryPolicyConfiguration : IEntityTypeConfiguration<TimeEntryPolicy>
{
    public void Configure(EntityTypeBuilder<TimeEntryPolicy> builder)
    {
        builder.ToTable("TimeEntryPolicies");
        builder.HasKey(policy => policy.Id);

        builder.Property(policy => policy.EffectiveFrom).HasColumnType("date").IsRequired();
        builder.Property(policy => policy.DailyTargetHours).HasPrecision(6, 2).IsRequired();
        builder.Property(policy => policy.LunchDeductionOver6H).HasPrecision(4, 2).IsRequired();
        builder.Property(policy => policy.LunchDeductionOver4H).HasPrecision(4, 2).IsRequired();

        builder.HasOne(policy => policy.Branch)
            .WithMany()
            .HasForeignKey(policy => policy.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        // One active policy per (branch, effective date): a second same-day change mutates
        // the day's row in place, keeping per-date resolution unambiguous.
        builder.HasIndex(policy => new { policy.BranchId, policy.EffectiveFrom })
            .IsUnique()
            .HasDatabaseName("IX_TimeEntryPolicies_BranchId_EffectiveFrom")
            .HasFilter("\"Active\" = true");
    }
}

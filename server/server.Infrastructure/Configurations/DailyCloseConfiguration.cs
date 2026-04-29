using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class DailyCloseConfiguration : IEntityTypeConfiguration<DailyClose>
{
    public void Configure(EntityTypeBuilder<DailyClose> builder)
    {
        builder.ToTable("DailyCloses");
        builder.HasKey(dailyClose => dailyClose.Id);

        builder.Property(dailyClose => dailyClose.Date).HasColumnType("date").IsRequired();
        builder.Property(dailyClose => dailyClose.Status).HasConversion<short>().IsRequired();
        builder.Property(dailyClose => dailyClose.SubmittedAt).HasColumnType("timestamp with time zone").IsRequired(false);
        builder.Property(dailyClose => dailyClose.ApprovedAt).HasColumnType("timestamp with time zone").IsRequired(false);
        builder.Property(dailyClose => dailyClose.RejectionReason).HasMaxLength(500).IsRequired(false);
        builder.Property(dailyClose => dailyClose.Notes).HasMaxLength(1000).IsRequired(false);
        builder.Property(dailyClose => dailyClose.UpdatedAt).HasColumnType("timestamp with time zone").IsRequired(false);
        builder.Property(dailyClose => dailyClose.UpdatedByUserId).IsRequired(false);

        builder.HasOne(dailyClose => dailyClose.Account)
            .WithMany()
            .HasForeignKey(dailyClose => dailyClose.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(dailyClose => dailyClose.SubmittedByOperator)
            .WithMany()
            .HasForeignKey(dailyClose => dailyClose.SubmittedByOperatorId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(dailyClose => dailyClose.ApprovedByUser)
            .WithMany()
            .HasForeignKey(dailyClose => dailyClose.ApprovedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(dailyClose => dailyClose.Branch)
            .WithMany()
            .HasForeignKey(dailyClose => dailyClose.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(dailyClose => new { dailyClose.BranchId, dailyClose.AccountId, dailyClose.Date })
            .IsUnique()
            .HasDatabaseName("IX_DailyCloses_BranchId_AccountId_Date")
            .HasFilter("\"Active\" = true");

        builder.HasIndex(dailyClose => new { dailyClose.BranchId, dailyClose.Date, dailyClose.AccountId });
        builder.HasIndex(dailyClose => new { dailyClose.BranchId, dailyClose.Status });
        builder.HasIndex(dailyClose => new { dailyClose.BranchId, dailyClose.AccountId, dailyClose.Status });
    }
}

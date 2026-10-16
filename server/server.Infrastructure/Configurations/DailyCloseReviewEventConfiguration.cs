using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class DailyCloseReviewEventConfiguration : IEntityTypeConfiguration<DailyCloseReviewEvent>
{
    public void Configure(EntityTypeBuilder<DailyCloseReviewEvent> builder)
    {
        builder.ToTable("DailyCloseReviewEvents");
        builder.HasKey(reviewEvent => reviewEvent.Id);

        builder.Property(reviewEvent => reviewEvent.Type).HasConversion<short>().IsRequired();
        builder.Property(reviewEvent => reviewEvent.OccurredAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(reviewEvent => reviewEvent.ActorUserName).HasMaxLength(255).IsRequired();
        builder.Property(reviewEvent => reviewEvent.ActorOperatorName).HasMaxLength(255).IsRequired(false);
        builder.Property(reviewEvent => reviewEvent.RejectionReason).HasMaxLength(500).IsRequired(false);
        builder.Property(reviewEvent => reviewEvent.SourceVersion).IsRequired(false);
        builder.Property(reviewEvent => reviewEvent.AffectedStatusBefore).HasConversion<short?>().IsRequired(false);
        builder.Property(reviewEvent => reviewEvent.AffectedStatusAfter).HasConversion<short?>().IsRequired(false);

        builder.HasOne(reviewEvent => reviewEvent.Branch)
            .WithMany()
            .HasForeignKey(reviewEvent => reviewEvent.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(reviewEvent => reviewEvent.DailyClose)
            .WithMany()
            .HasForeignKey(reviewEvent => reviewEvent.DailyCloseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(reviewEvent => reviewEvent.ActorUser)
            .WithMany()
            .HasForeignKey(reviewEvent => reviewEvent.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(reviewEvent => reviewEvent.ActorOperator)
            .WithMany()
            .HasForeignKey(reviewEvent => reviewEvent.ActorOperatorId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(reviewEvent => reviewEvent.SourceDailyClose)
            .WithMany()
            .HasForeignKey(reviewEvent => reviewEvent.SourceDailyCloseId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(reviewEvent => reviewEvent.PreviousSourceDailyClose)
            .WithMany()
            .HasForeignKey(reviewEvent => reviewEvent.PreviousSourceDailyCloseId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(reviewEvent => new { reviewEvent.BranchId, reviewEvent.DailyCloseId, reviewEvent.OccurredAt })
            .HasDatabaseName("IX_DailyCloseReviewEvents_BranchId_DailyCloseId_OccurredAt");
    }
}

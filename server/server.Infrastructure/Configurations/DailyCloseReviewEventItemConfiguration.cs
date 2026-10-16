using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class DailyCloseReviewEventItemConfiguration : IEntityTypeConfiguration<DailyCloseReviewEventItem>
{
    public void Configure(EntityTypeBuilder<DailyCloseReviewEventItem> builder)
    {
        builder.ToTable("DailyCloseReviewEventItems");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.ProductName).HasMaxLength(255).IsRequired();
        builder.Property(item => item.BeforeValue).HasColumnType("numeric(14,2)").IsRequired(false);
        builder.Property(item => item.AfterValue).HasColumnType("numeric(14,2)").IsRequired(false);

        builder.HasOne(item => item.DailyCloseReviewEvent)
            .WithMany(reviewEvent => reviewEvent.Items)
            .HasForeignKey(item => item.DailyCloseReviewEventId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(item => item.Product)
            .WithMany()
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.DailyCloseReviewEventId, item.ProductId })
            .IsUnique()
            .HasDatabaseName("IX_DailyCloseReviewEventItems_EventId_ProductId");
    }
}

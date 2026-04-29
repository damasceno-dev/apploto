using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class DailyCloseItemConfiguration : IEntityTypeConfiguration<DailyCloseItem>
{
    public void Configure(EntityTypeBuilder<DailyCloseItem> builder)
    {
        builder.ToTable("DailyCloseItems");
        builder.HasKey(dailyCloseItem => dailyCloseItem.Id);

        builder.Property(dailyCloseItem => dailyCloseItem.Value).HasColumnType("numeric(14,2)").IsRequired();

        builder.HasOne(dailyCloseItem => dailyCloseItem.DailyClose)
            .WithMany(dailyClose => dailyClose.Items)
            .HasForeignKey(dailyCloseItem => dailyCloseItem.DailyCloseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(dailyCloseItem => dailyCloseItem.Product)
            .WithMany()
            .HasForeignKey(dailyCloseItem => dailyCloseItem.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(dailyCloseItem => new { dailyCloseItem.DailyCloseId, dailyCloseItem.ProductId })
            .IsUnique()
            .HasDatabaseName("IX_DailyCloseItems_DailyCloseId_ProductId")
            .HasFilter("\"Active\" = true");
    }
}

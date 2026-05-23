using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;
using server.Domain.Entities.Enums;

namespace server.Infrastructure.Configurations;

internal class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> builder)
    {
        builder.ToTable("Holidays");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Date).HasColumnType("date").IsRequired();
        builder.Property(h => h.Description).HasMaxLength(500).IsRequired(false);
        builder.Property(h => h.Source)
            .HasConversion<short>()
            .IsRequired()
            .HasDefaultValue(HolidaySource.Manual);

        builder.HasOne(h => h.Branch)
            .WithMany()
            .HasForeignKey(h => h.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(h => new { h.BranchId, h.Date })
            .IsUnique()
            .HasDatabaseName("IX_Holidays_BranchId_Date")
            .HasFilter("\"Active\" = true");
    }
}

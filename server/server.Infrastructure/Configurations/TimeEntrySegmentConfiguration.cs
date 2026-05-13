using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class TimeEntrySegmentConfiguration : IEntityTypeConfiguration<TimeEntrySegment>
{
    public void Configure(EntityTypeBuilder<TimeEntrySegment> builder)
    {
        builder.ToTable("TimeEntrySegments");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.ClockIn).HasColumnType("timestamp without time zone").IsRequired();
        builder.Property(s => s.ClockOut).HasColumnType("timestamp without time zone").IsRequired(false);
        builder.Property(s => s.UpdatedAt).HasColumnType("timestamp with time zone").IsRequired(false);
        builder.Property(s => s.UpdatedByUserId).IsRequired(false);

        builder.HasOne(s => s.TimeEntry)
            .WithMany(te => te.Segments)
            .HasForeignKey(s => s.TimeEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.UpdatedByUser)
            .WithMany()
            .HasForeignKey(s => s.UpdatedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.TimeEntryId)
            .HasDatabaseName("IX_TimeEntrySegments_TimeEntryId_OpenShift")
            .IsUnique()
            .HasFilter("\"ClockOut\" IS NULL AND \"Active\" = true");

        builder.HasIndex(s => new { s.TimeEntryId, s.ClockIn })
            .HasDatabaseName("IX_TimeEntrySegments_TimeEntryId_ClockIn");
    }
}

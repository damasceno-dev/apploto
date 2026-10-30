using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class SessionOpeningDiscrepancyConfiguration : IEntityTypeConfiguration<SessionOpeningDiscrepancy>
{
    public void Configure(EntityTypeBuilder<SessionOpeningDiscrepancy> builder)
    {
        builder.ToTable("SessionOpeningDiscrepancies");
        builder.HasKey(discrepancy => discrepancy.Id);

        builder.Property(discrepancy => discrepancy.Version).IsRowVersion();
        builder.Property(discrepancy => discrepancy.SourceKind).HasConversion<short>().IsRequired();
        builder.Property(discrepancy => discrepancy.Attribution).HasConversion<short>().IsRequired();
        builder.Property(discrepancy => discrepancy.CurrentTotalDelta).HasColumnType("numeric(14,2)").IsRequired();

        builder.HasOne(discrepancy => discrepancy.SessionOpening)
            .WithMany()
            .HasForeignKey(discrepancy => discrepancy.SessionOpeningId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(discrepancy => discrepancy.Session)
            .WithMany()
            .HasForeignKey(discrepancy => discrepancy.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<SessionHandover>()
            .WithMany()
            .HasForeignKey(discrepancy => discrepancy.HandoverId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DailyClose>()
            .WithMany()
            .HasForeignKey(discrepancy => discrepancy.PredecessorSessionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(discrepancy => discrepancy.SessionOpeningId).IsUnique();
        builder.HasIndex(discrepancy => new { discrepancy.BranchId, discrepancy.Attribution });
    }
}

internal class SessionOpeningDiscrepancyRevisionConfiguration : IEntityTypeConfiguration<SessionOpeningDiscrepancyRevision>
{
    public void Configure(EntityTypeBuilder<SessionOpeningDiscrepancyRevision> builder)
    {
        builder.ToTable("SessionOpeningDiscrepancyRevisions");
        builder.HasKey(revision => revision.Id);

        builder.Property(revision => revision.ProposedTotal).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(revision => revision.AcknowledgedTotal).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(revision => revision.TotalDelta).HasColumnType("numeric(14,2)").IsRequired();

        builder.HasOne(revision => revision.Discrepancy)
            .WithMany(discrepancy => discrepancy.Revisions)
            .HasForeignKey(revision => revision.DiscrepancyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(revision => revision.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(revision => new { revision.DiscrepancyId, revision.Revision }).IsUnique();
    }
}

internal class SessionOpeningDiscrepancyRevisionItemConfiguration
    : IEntityTypeConfiguration<SessionOpeningDiscrepancyRevisionItem>
{
    public void Configure(EntityTypeBuilder<SessionOpeningDiscrepancyRevisionItem> builder)
    {
        builder.ToTable("SessionOpeningDiscrepancyRevisionItems");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.ProposedValue).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(item => item.AcknowledgedValue).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(item => item.Delta).HasColumnType("numeric(14,2)").IsRequired();

        builder.HasOne(item => item.Revision)
            .WithMany(revision => revision.Items)
            .HasForeignKey(item => item.RevisionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.RevisionId, item.ProductId }).IsUnique();
    }
}

internal class OpeningDiscrepancyAttributionEventConfiguration
    : IEntityTypeConfiguration<OpeningDiscrepancyAttributionEvent>
{
    public void Configure(EntityTypeBuilder<OpeningDiscrepancyAttributionEvent> builder)
    {
        builder.ToTable("OpeningDiscrepancyAttributionEvents");
        builder.HasKey(attribution => attribution.Id);

        builder.Property(attribution => attribution.Target).HasConversion<short>().IsRequired();
        builder.Property(attribution => attribution.Reason).HasMaxLength(500).IsRequired();

        builder.HasOne(attribution => attribution.Discrepancy)
            .WithMany()
            .HasForeignKey(attribution => attribution.DiscrepancyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(attribution => attribution.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<OpeningDiscrepancyAttributionEvent>()
            .WithMany()
            .HasForeignKey(attribution => attribution.SupersedesEventId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(attribution => new { attribution.DiscrepancyId, attribution.CreatedAt });

        // Unattributed is the initial state, never an attribution target.
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_OpeningDiscrepancyAttributionEvents_Target",
            "\"Target\" IN (1, 2, 3)"));
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class SessionOpeningConfiguration : IEntityTypeConfiguration<SessionOpening>
{
    public void Configure(EntityTypeBuilder<SessionOpening> builder)
    {
        builder.ToTable("SessionOpenings");
        builder.HasKey(opening => opening.Id);

        builder.Property(opening => opening.EvidenceKind).HasConversion<short>().IsRequired();
        builder.Property(opening => opening.SourceKind).HasConversion<short>().IsRequired();
        builder.Property(opening => opening.AcknowledgedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(opening => opening.Reason).HasMaxLength(500).IsRequired(false);

        builder.HasOne(opening => opening.Session)
            .WithOne(session => session.Opening)
            .HasForeignKey<SessionOpening>(opening => opening.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(opening => opening.AcknowledgedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Operator>()
            .WithMany()
            .HasForeignKey(opening => opening.AcknowledgedByOperatorId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<SessionHandover>()
            .WithMany()
            .HasForeignKey(opening => opening.HandoverId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(opening => opening.SessionId).IsUnique();
    }
}

internal class SessionOpeningItemConfiguration : IEntityTypeConfiguration<SessionOpeningItem>
{
    public void Configure(EntityTypeBuilder<SessionOpeningItem> builder)
    {
        builder.ToTable("SessionOpeningItems");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.ProposedValue).HasColumnType("numeric(14,2)").IsRequired(false);
        builder.Property(item => item.AcknowledgedValue).HasColumnType("numeric(14,2)").IsRequired();

        builder.HasOne(item => item.SessionOpening)
            .WithMany(opening => opening.Items)
            .HasForeignKey(item => item.SessionOpeningId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(item => item.Product)
            .WithMany()
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.SessionOpeningId, item.ProductId }).IsUnique();

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_SessionOpeningItems_NonNegative",
            "\"AcknowledgedValue\" >= 0 AND (\"ProposedValue\" IS NULL OR \"ProposedValue\" >= 0)"));
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class SessionHandoverConfiguration : IEntityTypeConfiguration<SessionHandover>
{
    public void Configure(EntityTypeBuilder<SessionHandover> builder)
    {
        builder.ToTable("SessionHandovers");
        builder.HasKey(handover => handover.Id);

        builder.Property(handover => handover.Version).IsRowVersion();
        builder.Property(handover => handover.State).HasConversion<short>().IsRequired();
        builder.Property(handover => handover.OfferedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(handover => handover.ResolvedAt).HasColumnType("timestamp with time zone").IsRequired(false);
        builder.Property(handover => handover.Reason).HasMaxLength(500).IsRequired(false);

        builder.HasOne(handover => handover.OutgoingSession)
            .WithMany()
            .HasForeignKey(handover => handover.OutgoingSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(handover => handover.IncomingSession)
            .WithMany()
            .HasForeignKey(handover => handover.IncomingSessionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(handover => handover.IntendedIncomingOperator)
            .WithMany()
            .HasForeignKey(handover => handover.IntendedIncomingOperatorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(handover => handover.OfferedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(handover => handover.IncomingSessionId)
            .IsUnique()
            .HasFilter("\"IncomingSessionId\" IS NOT NULL");

        // One unresolved (Offered or Disputed) offer per outgoing session.
        builder.HasIndex(handover => handover.OutgoingSessionId)
            .IsUnique()
            .HasDatabaseName("IX_SessionHandovers_OutgoingSessionId_Unresolved")
            .HasFilter("\"State\" IN (0, 2)");
    }
}

internal class SessionHandoverItemConfiguration : IEntityTypeConfiguration<SessionHandoverItem>
{
    public void Configure(EntityTypeBuilder<SessionHandoverItem> builder)
    {
        builder.ToTable("SessionHandoverItems");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Value).HasColumnType("numeric(14,2)").IsRequired();

        builder.HasOne(item => item.SessionHandover)
            .WithMany(handover => handover.Items)
            .HasForeignKey(item => item.SessionHandoverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.SessionHandoverId, item.ProductId }).IsUnique();
    }
}

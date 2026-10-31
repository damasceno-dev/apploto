using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class SessionFieldEffectConfiguration : IEntityTypeConfiguration<SessionFieldEffect>
{
    public void Configure(EntityTypeBuilder<SessionFieldEffect> builder)
    {
        builder.ToTable("SessionFieldEffects");
        builder.HasKey(effect => effect.Id);

        builder.Property(effect => effect.Field).HasConversion<short>().IsRequired();
        builder.Property(effect => effect.Value).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(effect => effect.UpdatedAt).HasColumnType("timestamp with time zone").IsRequired(false);

        builder.HasOne(effect => effect.Session)
            .WithMany()
            .HasForeignKey(effect => effect.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(effect => effect.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(effect => effect.CurrentTransaction)
            .WithMany()
            .HasForeignKey(effect => effect.CurrentTransactionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(effect => effect.UpdatedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // The role slot: one per session and field. Repeated or concurrent saves meet this index, never a second slot.
        builder.HasIndex(effect => new { effect.SessionId, effect.Field }).IsUnique();
        builder.HasIndex(effect => effect.CurrentTransactionId)
            .IsUnique()
            .HasFilter("\"CurrentTransactionId\" IS NOT NULL");

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_SessionFieldEffects_FieldValid",
            "\"Field\" IN (1, 2, 3)"));
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_SessionFieldEffects_FeeNonNegative",
            "\"Field\" = 1 OR \"Value\" >= 0"));
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_SessionFieldEffects_EffectMatchesValue",
            "(\"Value\" = 0) = (\"CurrentTransactionId\" IS NULL)"));
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_SessionFieldEffects_RevisionPositive",
            "\"Revision\" > 0"));
    }
}

internal class SessionFieldEffectRevisionConfiguration : IEntityTypeConfiguration<SessionFieldEffectRevision>
{
    public void Configure(EntityTypeBuilder<SessionFieldEffectRevision> builder)
    {
        builder.ToTable("SessionFieldEffectRevisions");
        builder.HasKey(revision => revision.Id);

        builder.Property(revision => revision.PreviousValue).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(revision => revision.Value).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(revision => revision.Origin).HasConversion<short>().IsRequired();
        builder.Property(revision => revision.RecordedAt).HasColumnType("timestamp with time zone").IsRequired();

        builder.HasOne(revision => revision.SessionFieldEffect)
            .WithMany(effect => effect.Revisions)
            .HasForeignKey(revision => revision.SessionFieldEffectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Transaction>()
            .WithMany()
            .HasForeignKey(revision => revision.CreatedTransactionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Transaction>()
            .WithMany()
            .HasForeignKey(revision => revision.CancelledTransactionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(revision => revision.ActorUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(revision => new { revision.SessionFieldEffectId, revision.Revision }).IsUnique();
    }
}

internal class CustodyTransferConfiguration : IEntityTypeConfiguration<CustodyTransfer>
{
    public void Configure(EntityTypeBuilder<CustodyTransfer> builder)
    {
        builder.ToTable("CustodyTransfers");
        builder.HasKey(transfer => transfer.Id);

        builder.Property(transfer => transfer.AssetKind).HasConversion<short>().IsRequired();
        builder.Property(transfer => transfer.Value).HasColumnType("numeric(14,2)").IsRequired();

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(transfer => transfer.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(transfer => transfer.SourceAccount)
            .WithMany()
            .HasForeignKey(transfer => transfer.SourceAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(transfer => transfer.DestinationAccount)
            .WithMany()
            .HasForeignKey(transfer => transfer.DestinationAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DailyClose>()
            .WithMany()
            .HasForeignKey(transfer => transfer.SourceSessionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(transfer => transfer.SourceTransaction)
            .WithMany()
            .HasForeignKey(transfer => transfer.SourceTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(transfer => transfer.DestinationTransaction)
            .WithMany()
            .HasForeignKey(transfer => transfer.DestinationTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(transfer => transfer.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(transfer => transfer.SourceTransactionId).IsUnique();
        builder.HasIndex(transfer => transfer.DestinationTransactionId).IsUnique();
        builder.HasIndex(transfer => new { transfer.BranchId, transfer.DestinationAccountId });

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_CustodyTransfers_ValuePositive",
            "\"Value\" > 0"));
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_CustodyTransfers_DistinctAccounts",
            "\"SourceAccountId\" <> \"DestinationAccountId\""));
    }
}

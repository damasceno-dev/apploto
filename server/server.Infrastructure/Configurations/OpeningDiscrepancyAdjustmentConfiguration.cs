using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class OpeningDiscrepancyAdjustmentConfiguration : IEntityTypeConfiguration<OpeningDiscrepancyAdjustment>
{
    public void Configure(EntityTypeBuilder<OpeningDiscrepancyAdjustment> b)
    {
        b.ToTable("OpeningDiscrepancyAdjustments", t =>
        {
            t.HasCheckConstraint("CK_OpeningDiscrepancyAdjustments_Target", "\"Target\" IN (1,2,3)");
            t.HasCheckConstraint("CK_OpeningDiscrepancyAdjustments_Sequence", "\"Sequence\" > 0 AND \"ComparisonRevision\" > 0");
            t.HasCheckConstraint("CK_OpeningDiscrepancyAdjustments_Neutral", "(\"OperatorId\" IS NOT NULL OR \"SignedAmount\" = 0) AND (\"PreviousOperatorId\" IS NOT NULL OR \"PreviousSignedAmount\" = 0) AND (\"Target\" <> 3 OR (\"OperatorId\" IS NULL AND \"SignedAmount\" = 0))");
        });
        b.HasKey(a => a.Id);
        b.Property(a => a.SourceDate).HasColumnType("date");
        b.Property(a => a.PostedOn).HasColumnType("date");
        b.Property(a => a.Target).HasConversion<short>();
        b.Property(a => a.SignedAmount).HasColumnType("numeric(14,2)");
        b.Property(a => a.PreviousSignedAmount).HasColumnType("numeric(14,2)");
        b.Property(a => a.Reason).HasMaxLength(500).IsRequired();
        b.HasIndex(a => new { a.DiscrepancyId, a.Sequence }).IsUnique();
        b.HasIndex(a => new { a.BranchId, a.PostedOn });
        b.HasOne<SessionOpeningDiscrepancy>().WithMany().HasForeignKey(a => a.DiscrepancyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OpeningDiscrepancyAdjustment>().WithMany().HasForeignKey(a => a.PreviousAdjustmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OpeningDiscrepancyAttributionEvent>().WithMany().HasForeignKey(a => a.PriorAttributionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DailyClose>().WithMany().HasForeignKey(a => a.SessionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Operator>().WithMany().HasForeignKey(a => a.OperatorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Operator>().WithMany().HasForeignKey(a => a.PreviousOperatorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(a => a.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

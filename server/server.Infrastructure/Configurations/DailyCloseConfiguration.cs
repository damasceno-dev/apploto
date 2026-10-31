using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class DailyCloseConfiguration : IEntityTypeConfiguration<DailyClose>
{
    public void Configure(EntityTypeBuilder<DailyClose> builder)
    {
        builder.ToTable("DailyCloses");
        builder.HasKey(dailyClose => dailyClose.Id);

        builder.Property(dailyClose => dailyClose.Version).IsRowVersion();
        builder.Property(dailyClose => dailyClose.Date).HasColumnType("date").IsRequired();
        builder.Property(dailyClose => dailyClose.Status).HasConversion<short>().IsRequired();
        builder.Property(dailyClose => dailyClose.SubmittedAt).HasColumnType("timestamp with time zone").IsRequired(false);
        builder.Property(dailyClose => dailyClose.ApprovedAt).HasColumnType("timestamp with time zone").IsRequired(false);
        builder.Property(dailyClose => dailyClose.RejectionReason).HasMaxLength(500).IsRequired(false);
        builder.Property(dailyClose => dailyClose.Notes).HasMaxLength(1000).IsRequired(false);
        builder.Property(dailyClose => dailyClose.ItemsFirstRecordedAt).HasColumnType("timestamp with time zone").IsRequired(false);
        builder.Property(dailyClose => dailyClose.OpeningRecheckRequiredAt).HasColumnType("timestamp with time zone").IsRequired(false);
        builder.Property(dailyClose => dailyClose.UpdatedAt).HasColumnType("timestamp with time zone").IsRequired(false);
        builder.Property(dailyClose => dailyClose.UpdatedByUserId).IsRequired(false);
        builder.Property(dailyClose => dailyClose.ReviewHistoryComplete).IsRequired();

        builder.HasOne(dailyClose => dailyClose.Account)
            .WithMany()
            .HasForeignKey(dailyClose => dailyClose.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(dailyClose => dailyClose.OpenedByUser)
            .WithMany()
            .HasForeignKey(dailyClose => dailyClose.OpenedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(dailyClose => dailyClose.RecordedByUser)
            .WithMany()
            .HasForeignKey(dailyClose => dailyClose.RecordedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(dailyClose => dailyClose.RecordedByOperator)
            .WithMany()
            .HasForeignKey(dailyClose => dailyClose.RecordedByOperatorId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(dailyClose => dailyClose.SubmittedByUser)
            .WithMany()
            .HasForeignKey(dailyClose => dailyClose.SubmittedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(dailyClose => dailyClose.SubmittedByOperator)
            .WithMany()
            .HasForeignKey(dailyClose => dailyClose.SubmittedByOperatorId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(dailyClose => dailyClose.ApprovedByUser)
            .WithMany()
            .HasForeignKey(dailyClose => dailyClose.ApprovedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(dailyClose => dailyClose.OpeningRecheckTriggeredByDailyClose)
            .WithMany()
            .HasForeignKey(dailyClose => dailyClose.OpeningRecheckTriggeredByDailyCloseId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(dailyClose => dailyClose.OpeningRecheckTriggeredByUser)
            .WithMany()
            .HasForeignKey(dailyClose => dailyClose.OpeningRecheckTriggeredByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(dailyClose => dailyClose.Branch)
            .WithMany()
            .HasForeignKey(dailyClose => dailyClose.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(dailyClose => dailyClose.MappingState).HasConversion<short>().IsRequired();
        builder.Property(dailyClose => dailyClose.Origin).HasConversion<short>().IsRequired();
        builder.Property(dailyClose => dailyClose.CustodyState).HasConversion<short?>().IsRequired(false);
        builder.Property(dailyClose => dailyClose.StartedAt).HasColumnType("timestamp with time zone").IsRequired(false);
        builder.Property(dailyClose => dailyClose.EndedAt).HasColumnType("timestamp with time zone").IsRequired(false);
        builder.Property(dailyClose => dailyClose.LedgerRevision).IsRequired();
        builder.Property(dailyClose => dailyClose.CountRevision).IsRequired();
        builder.Property(dailyClose => dailyClose.OpeningRevision).IsRequired();
        builder.Property(dailyClose => dailyClose.CurrentConfirmationToken)
            .HasDefaultValueSql("gen_random_uuid()")
            .IsRequired();
        builder.Property(dailyClose => dailyClose.LastAttemptNumber).IsRequired();

        builder.HasOne<SessionCountAttempt>()
            .WithMany()
            .HasForeignKey(dailyClose => dailyClose.SubmittedAttemptId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<SessionCountAttempt>()
            .WithMany()
            .HasForeignKey(dailyClose => dailyClose.RecountStartedForAttemptId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(dailyClose => dailyClose.CustodyHistoryComplete).IsRequired();

        builder.HasOne(dailyClose => dailyClose.CustodianOperator)
            .WithMany()
            .HasForeignKey(dailyClose => dailyClose.CustodianOperatorId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(dailyClose => dailyClose.Terminal)
            .WithMany()
            .HasForeignKey(dailyClose => dailyClose.TerminalId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // Session identity replaces the account/day uniqueness (M9.6 Phase 2.2): one Sequence per resolved cashbox
        // session and one Active custody per cashbox. Historical Draft corrections coexist with today's custody.
        builder.HasIndex(dailyClose => new { dailyClose.BranchId, dailyClose.AccountId, dailyClose.Sequence })
            .IsUnique()
            .HasDatabaseName("IX_DailyCloses_BranchId_AccountId_Sequence")
            .HasFilter("\"MappingState\" = 0");

        builder.HasIndex(dailyClose => new { dailyClose.BranchId, dailyClose.AccountId })
            .IsUnique()
            .HasDatabaseName("IX_DailyCloses_BranchId_AccountId_ActiveCustody")
            .HasFilter("\"Active\" = true AND \"MappingState\" = 0 AND \"CustodyState\" = 0");

        builder.HasIndex(dailyClose => new { dailyClose.BranchId, dailyClose.Date, dailyClose.AccountId });
        builder.HasIndex(dailyClose => new { dailyClose.BranchId, dailyClose.Status });
        builder.HasIndex(dailyClose => new { dailyClose.BranchId, dailyClose.AccountId, dailyClose.Status });

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_DailyCloses_RecordingIdentityMatchesFirstCount",
            "(\"ItemsFirstRecordedAt\" IS NULL AND \"RecordedByUserId\" IS NULL AND \"RecordedByOperatorId\" IS NULL) OR " +
            "(\"ItemsFirstRecordedAt\" IS NOT NULL AND \"RecordedByUserId\" IS NOT NULL)"));
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_DailyCloses_ReadySessionIdentity",
            "\"MappingState\" <> 0 OR (\"CustodyState\" IS NOT NULL AND \"Sequence\" IS NOT NULL AND " +
            "\"StartedAt\" IS NOT NULL AND \"CustodianOperatorId\" IS NOT NULL)"));
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_DailyCloses_CustodyStateMatchesEnd",
            "\"CustodyState\" IS NULL OR ((\"CustodyState\" = 0) = (\"EndedAt\" IS NULL))"));
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_DailyCloses_IntervalOrdered",
            "\"EndedAt\" IS NULL OR \"StartedAt\" IS NULL OR \"EndedAt\" > \"StartedAt\""));
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_DailyCloses_RevisionsNonNegative",
            "\"LedgerRevision\" >= 0 AND \"CountRevision\" >= 0 AND \"OpeningRevision\" >= 0 AND \"LastAttemptNumber\" >= 0"));
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_DailyCloses_SequencePositive",
            "\"Sequence\" IS NULL OR \"Sequence\" > 0"));
    }
}

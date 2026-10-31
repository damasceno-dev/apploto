using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class SessionEventConfiguration : IEntityTypeConfiguration<SessionEvent>
{
    public void Configure(EntityTypeBuilder<SessionEvent> builder)
    {
        builder.ToTable("SessionEvents");
        builder.HasKey(sessionEvent => sessionEvent.Id);

        builder.Property(sessionEvent => sessionEvent.Kind).HasConversion<short>().IsRequired();
        builder.Property(sessionEvent => sessionEvent.ActorName).HasMaxLength(255).IsRequired();
        builder.Property(sessionEvent => sessionEvent.Reason).HasMaxLength(500).IsRequired(false);
        builder.Property(sessionEvent => sessionEvent.Details).HasColumnType("jsonb").IsRequired();

        builder.HasOne(sessionEvent => sessionEvent.Session)
            .WithMany()
            .HasForeignKey(sessionEvent => sessionEvent.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(sessionEvent => sessionEvent.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(sessionEvent => new { sessionEvent.SessionId, sessionEvent.CreatedAt });

        // One CountConferred event per attempt, whatever request keys reached it (§3.30).
        builder.HasOne(sessionEvent => sessionEvent.Attempt)
            .WithMany()
            .HasForeignKey(sessionEvent => sessionEvent.AttemptId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(sessionEvent => sessionEvent.AttemptId)
            .IsUnique()
            .HasDatabaseName("IX_SessionEvents_AttemptId_CountConferred")
            .HasFilter("\"Kind\" = 13");
    }
}

internal class SessionCountAttemptConfiguration : IEntityTypeConfiguration<SessionCountAttempt>
{
    public void Configure(EntityTypeBuilder<SessionCountAttempt> builder)
    {
        builder.ToTable("SessionCountAttempts");
        builder.HasKey(attempt => attempt.Id);

        builder.Property(attempt => attempt.Kind).HasConversion<short>().IsRequired();
        builder.Property(attempt => attempt.TotalOpening).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(attempt => attempt.TotalIn).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(attempt => attempt.TotalOut).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(attempt => attempt.ExpectedClosing).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(attempt => attempt.TotalClosing).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(attempt => attempt.Difference).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(attempt => attempt.ConferredByName).HasMaxLength(255).IsRequired();
        builder.Property(attempt => attempt.CommandKey).HasMaxLength(128).IsRequired();

        builder.HasOne(attempt => attempt.Session)
            .WithMany()
            .HasForeignKey(attempt => attempt.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(attempt => attempt.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(attempt => attempt.ConferredByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Operator>()
            .WithMany()
            .HasForeignKey(attempt => attempt.ConferredByOperatorId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(attempt => new { attempt.SessionId, attempt.ConfirmationToken }).IsUnique();
        builder.HasIndex(attempt => new { attempt.SessionId, attempt.AttemptNumber }).IsUnique();

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_SessionCountAttempts_NumberPositive",
            "\"AttemptNumber\" > 0"));
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_SessionCountAttempts_DifferenceMatches",
            "\"Difference\" = \"TotalClosing\" - \"ExpectedClosing\" AND " +
            "\"ExpectedClosing\" = \"TotalOpening\" + \"TotalIn\" - \"TotalOut\""));
    }
}

internal class SessionCountAttemptItemConfiguration : IEntityTypeConfiguration<SessionCountAttemptItem>
{
    public void Configure(EntityTypeBuilder<SessionCountAttemptItem> builder)
    {
        builder.ToTable("SessionCountAttemptItems");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.OpeningValue).HasColumnType("numeric(14,2)").IsRequired(false);
        builder.Property(item => item.CountedValue).HasColumnType("numeric(14,2)").IsRequired(false);

        builder.HasOne(item => item.Attempt)
            .WithMany(attempt => attempt.Items)
            .HasForeignKey(item => item.AttemptId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(item => item.Product)
            .WithMany()
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.AttemptId, item.ProductId }).IsUnique();
    }
}

internal class CustodyMappingIssueConfiguration : IEntityTypeConfiguration<CustodyMappingIssue>
{
    public void Configure(EntityTypeBuilder<CustodyMappingIssue> builder)
    {
        builder.ToTable("CustodyMappingIssues");
        builder.HasKey(issue => issue.Id);

        builder.Property(issue => issue.SourceKind).HasConversion<short>().IsRequired();
        builder.Property(issue => issue.IssueCode).HasConversion<short>().IsRequired();
        builder.Property(issue => issue.Status).HasConversion<short>().IsRequired();
        builder.Property(issue => issue.SourceDigest).HasMaxLength(64).IsRequired();
        builder.Property(issue => issue.ResolutionReason).HasMaxLength(500).IsRequired(false);
        builder.Property(issue => issue.ResolvedAt).HasColumnType("timestamp with time zone").IsRequired(false);

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(issue => issue.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(issue => new { issue.SourceKind, issue.SourceId, issue.IssueCode }).IsUnique();
        builder.HasIndex(issue => new { issue.BranchId, issue.Status });
    }
}

internal class OpeningCountConfiguration : IEntityTypeConfiguration<OpeningCount>
{
    public void Configure(EntityTypeBuilder<OpeningCount> builder)
    {
        builder.ToTable("OpeningCounts");
        builder.HasKey(openingCount => openingCount.Id);

        builder.Property(openingCount => openingCount.Version).IsRowVersion();
        builder.Property(openingCount => openingCount.AsOfDate).HasColumnType("date").IsRequired();

        builder.HasOne(openingCount => openingCount.Account)
            .WithMany()
            .HasForeignKey(openingCount => openingCount.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(openingCount => openingCount.RecordedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(openingCount => new { openingCount.BranchId, openingCount.AccountId })
            .IsUnique()
            .HasDatabaseName("IX_OpeningCounts_BranchId_AccountId_Current")
            .HasFilter("\"Active\" = true");
        builder.HasIndex(openingCount => new { openingCount.AccountId, openingCount.Revision }).IsUnique();
    }
}

internal class OpeningCountItemConfiguration : IEntityTypeConfiguration<OpeningCountItem>
{
    public void Configure(EntityTypeBuilder<OpeningCountItem> builder)
    {
        builder.ToTable("OpeningCountItems");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Value).HasColumnType("numeric(14,2)").IsRequired();

        builder.HasOne(item => item.OpeningCount)
            .WithMany(openingCount => openingCount.Items)
            .HasForeignKey(item => item.OpeningCountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.OpeningCountId, item.ProductId }).IsUnique();

        builder.ToTable(table => table.HasCheckConstraint("CK_OpeningCountItems_NonNegative", "\"Value\" >= 0"));
    }
}

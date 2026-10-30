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

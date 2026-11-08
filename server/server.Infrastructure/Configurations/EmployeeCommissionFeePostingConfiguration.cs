using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class EmployeeCommissionFeePostingConfiguration : IEntityTypeConfiguration<EmployeeCommissionFeePosting>
{
    public void Configure(EntityTypeBuilder<EmployeeCommissionFeePosting> b)
    {
        b.ToTable("EmployeeCommissionFeePostings");
        b.HasKey(x => x.Id);
        b.Property(x => x.SourceDate).HasColumnType("date");
        b.Property(x => x.PostedOn).HasColumnType("date");
        b.Property(x => x.SignedAmount).HasPrecision(14, 2);
        b.Property(x => x.Component).HasConversion<short>();
        b.Property(x => x.ClassificationProvenance).HasConversion<short>();
        b.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Operator>().WithMany().HasForeignKey(x => x.OperatorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DailyClose>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SessionCountAttempt>().WithMany().HasForeignKey(x => x.AttemptId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Transaction>().WithMany().HasForeignKey(x => x.TransactionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.SessionId, x.AttemptId, x.TransactionId, x.Component, x.SourceDate, x.ClassificationRevision }).HasDatabaseName("UX_CommissionFeePosting_AttemptTransactionSource").IsUnique();
        b.HasIndex(x => new { x.BranchId, x.OperatorId, x.PostedOn });
    }
}

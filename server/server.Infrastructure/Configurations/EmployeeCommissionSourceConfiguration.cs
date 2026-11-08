using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;
namespace server.Infrastructure.Configurations;

internal class EmployeeCommissionSourceConfiguration : IEntityTypeConfiguration<EmployeeCommissionSource>
{
    public void Configure(EntityTypeBuilder<EmployeeCommissionSource> b)
    {
        b.ToTable("EmployeeCommissionSources");
        b.HasKey(x => x.Id);
        b.Property(x => x.Version).IsRowVersion();
        b.Property(x => x.PostedOn).HasColumnType("date");
        b.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        b.Property(x => x.Evidence).HasMaxLength(1000).IsRequired();
        b.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.BranchId, x.PostedOn });
        b.HasOne<EmployeeCommissionSource>().WithMany().HasForeignKey(x => x.PreviousId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.PreviousId).IsUnique().HasFilter("\"PreviousId\" IS NOT NULL");
        b.Property(x => x.SourceDate).HasColumnType("date");
        b.Property(x => x.SignedAmount).HasPrecision(14, 2).IsRequired();
        b.Property(x => x.ExternalIdentity).HasMaxLength(200);
        b.Property(x => x.SourceRole).HasMaxLength(200);
        b.Property(x => x.SignedContribution).HasPrecision(14, 2).IsRequired();
        b.HasOne<Operator>().WithMany().HasForeignKey(x => x.OperatorId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.BranchId, x.ExternalIdentity }).IsUnique().HasFilter("\"PreviousId\" IS NULL");
    }
}

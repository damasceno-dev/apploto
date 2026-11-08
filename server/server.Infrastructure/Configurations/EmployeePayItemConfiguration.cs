using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;
namespace server.Infrastructure.Configurations;

internal class EmployeePayItemConfiguration : IEntityTypeConfiguration<EmployeePayItem>
{
    public void Configure(EntityTypeBuilder<EmployeePayItem> b)
    {
        b.ToTable("EmployeePayItems");
        b.HasKey(x => x.Id);
        b.Property(x => x.CategoryName).HasMaxLength(200).IsRequired();
        b.Property(x => x.CommissionEvidenceJson).HasColumnType("text");
        b.Property(x => x.Version).IsRowVersion();
        b.Property(x => x.PostedOn).HasColumnType("date");
        b.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        b.Property(x => x.Evidence).HasMaxLength(1000).IsRequired();
        b.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.BranchId, x.PostedOn });
        b.HasOne<EmployeePayItem>().WithMany().HasForeignKey(x => x.PreviousId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.PreviousId).IsUnique().HasFilter("\"PreviousId\" IS NOT NULL");
        b.Property(x => x.SourceMonth).HasColumnType("date");
        b.Property(x => x.SourceDate).HasColumnType("date");
        b.Property(x => x.Amount).HasPrecision(14, 2).IsRequired();
        b.Property(x => x.Kind).HasConversion<short>();
        b.Property(x => x.DeductionPolicy).HasMaxLength(1000);
        b.Property(x => x.SignedContribution).HasPrecision(14, 2).IsRequired();
        b.Property(x => x.CommissionCalculationId).HasMaxLength(200);
        b.HasOne<Operator>().WithMany().HasForeignKey(x => x.OperatorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<EmployeePayCategory>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.BranchId, x.CommissionCalculationId }).IsUnique().HasFilter("\"CommissionCalculationId\" IS NOT NULL AND \"PreviousId\" IS NULL");
        b.HasIndex(x => new { x.BranchId, x.OperatorId, x.SourceMonth }).IsUnique()
            .HasFilter("\"CommissionCalculationId\" IS NOT NULL AND \"PreviousId\" IS NULL");
    }
}

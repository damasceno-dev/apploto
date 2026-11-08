using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;
namespace server.Infrastructure.Configurations;

internal class EmployeeCommissionRuleConfiguration : IEntityTypeConfiguration<EmployeeCommissionRule>
{
    public void Configure(EntityTypeBuilder<EmployeeCommissionRule> b)
    {
        b.ToTable("EmployeeCommissionRules");
        b.HasKey(x => x.Id);
        b.Property(x => x.Version).IsRowVersion();
        b.Property(x => x.PostedOn).HasColumnType("date");
        b.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        b.Property(x => x.Evidence).HasMaxLength(1000).IsRequired();
        b.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.BranchId, x.PostedOn });
        b.HasOne<EmployeeCommissionRule>().WithMany().HasForeignKey(x => x.PreviousId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.PreviousId).IsUnique().HasFilter("\"PreviousId\" IS NOT NULL");
        b.Property(x => x.EffectiveFrom).HasColumnType("date");
        b.Property(x => x.EffectiveTo).HasColumnType("date");
        b.Property(x => x.RatePercent).HasPrecision(14, 4).IsRequired();
        b.HasOne<Operator>().WithMany().HasForeignKey(x => x.OperatorId).OnDelete(DeleteBehavior.Restrict);
    }
}

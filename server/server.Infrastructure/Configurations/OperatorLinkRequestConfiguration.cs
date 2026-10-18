using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal sealed class OperatorLinkRequestConfiguration : IEntityTypeConfiguration<OperatorLinkRequest>
{
    public void Configure(EntityTypeBuilder<OperatorLinkRequest> b)
    {
        b.ToTable("OperatorLinkRequests");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.BranchId, x.UserId }).IsUnique().HasFilter("\"Active\" = TRUE AND \"Status\" = 0");
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ResolvedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

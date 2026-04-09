using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class BranchUserConfiguration : IEntityTypeConfiguration<BranchUser>
{
    public void Configure(EntityTypeBuilder<BranchUser> builder)
    {
        builder.ToTable("BranchUsers");
        builder.HasKey(branchUser => branchUser.Id);

        builder.HasIndex(branchUser => new { branchUser.UserId, branchUser.BranchId }).IsUnique();
    }
}

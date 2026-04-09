using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("Branches");
        builder.HasKey(branch => branch.Id);

        builder.Property(branch => branch.Name).HasMaxLength(255);
        builder.Property(branch => branch.Cnpj).HasMaxLength(18);
        builder.Property(branch => branch.Phone).HasMaxLength(20);

        builder.HasMany(branch => branch.BranchUsers)
            .WithOne(branchUser => branchUser.Branch)
            .HasForeignKey(branchUser => branchUser.BranchId);

        builder.HasMany(branch => branch.Categories)
            .WithOne(category => category.Branch)
            .HasForeignKey(category => category.BranchId);

        builder.HasMany(branch => branch.Products)
            .WithOne(product => product.Branch)
            .HasForeignKey(product => product.BranchId);

        builder.HasOne(branch => branch.Setting)
            .WithOne(setting => setting.Branch)
            .HasForeignKey<Setting>(setting => setting.BranchId);
    }
}

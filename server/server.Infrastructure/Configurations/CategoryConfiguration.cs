using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(category => category.Id);

        builder.Property(category => category.Name).HasMaxLength(255);

        builder.HasIndex(category => new { category.BranchId, category.Name })
            .IsUnique()
            .HasDatabaseName("IX_Categories_BranchId_Name")
            .HasFilter("\"Active\" = true");

        builder.HasMany(category => category.TransactionTypes)
            .WithOne(transactionType => transactionType.Category)
            .HasForeignKey(transactionType => transactionType.CategoryId);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class OperatorConfiguration : IEntityTypeConfiguration<Operator>
{
    public void Configure(EntityTypeBuilder<Operator> builder)
    {
        builder.ToTable("Operators");
        builder.HasKey(op => op.Id);

        builder.Property(op => op.Name).HasMaxLength(255).IsRequired();

        builder.HasIndex(op => op.BranchId);

        builder.HasIndex(op => new { op.BranchId, op.UserId })
            .IsUnique()
            .HasFilter("\"UserId\" IS NOT NULL AND \"Active\" = true");

        builder.HasOne(op => op.Branch)
            .WithMany()
            .HasForeignKey(op => op.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(op => op.User)
            .WithMany()
            .HasForeignKey(op => op.UserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class TerminalConfiguration : IEntityTypeConfiguration<Terminal>
{
    public void Configure(EntityTypeBuilder<Terminal> builder)
    {
        builder.ToTable("Terminals");
        builder.HasKey(terminal => terminal.Id);

        builder.Property(terminal => terminal.Name).HasMaxLength(255).IsRequired();
        builder.Property(terminal => terminal.ExternalReference).HasMaxLength(100).IsRequired(false);

        builder.HasOne(terminal => terminal.Branch)
            .WithMany()
            .HasForeignKey(terminal => terminal.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(terminal => new { terminal.BranchId, terminal.ExternalReference })
            .IsUnique()
            .HasDatabaseName("IX_Terminals_BranchId_ExternalReference")
            .HasFilter("\"ExternalReference\" IS NOT NULL");
    }
}

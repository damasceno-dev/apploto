using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("Clients");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(255).IsRequired();
        builder.Property(c => c.Phone).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Cpf).HasMaxLength(14).IsRequired(false);
        builder.Property(c => c.Cep).HasMaxLength(9).IsRequired(false);
        builder.Property(c => c.Address).HasMaxLength(500).IsRequired(false);
        builder.Property(c => c.PhoneSecondary).HasMaxLength(20).IsRequired(false);
        builder.Property(c => c.Notes).HasMaxLength(1000).IsRequired(false);
        builder.Property(c => c.Email).HasMaxLength(254).IsRequired(false);

        builder.HasOne(c => c.Branch)
            .WithMany()
            .HasForeignKey(c => c.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        // CPF uniqueness per branch — only enforced when CPF is present and client is active
        builder.HasIndex(c => new { c.BranchId, c.Cpf })
            .IsUnique()
            .HasFilter("\"Cpf\" IS NOT NULL AND \"Active\" = true");
    }
}

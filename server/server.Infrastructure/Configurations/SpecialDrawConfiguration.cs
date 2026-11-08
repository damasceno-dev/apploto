using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;
namespace server.Infrastructure.Configurations;

internal class SpecialDrawConfiguration : IEntityTypeConfiguration<SpecialDraw>
{
    public void Configure(EntityTypeBuilder<SpecialDraw> b)
    {
        b.ToTable("SpecialDraws");
        b.HasKey(x => x.Id);
        b.Property(x => x.Version).IsRowVersion();
        b.Property(x => x.PostedOn).HasColumnType("date");
        b.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        b.Property(x => x.Evidence).HasMaxLength(1000).IsRequired();
        b.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.BranchId, x.PostedOn });
        b.HasOne<SpecialDraw>().WithMany().HasForeignKey(x => x.PreviousId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.PreviousId).IsUnique().HasFilter("\"PreviousId\" IS NOT NULL");
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Modality).HasMaxLength(200);
        b.Property(x => x.Contest).HasMaxLength(200);
        b.Property(x => x.PeriodFrom).HasColumnType("date");
        b.Property(x => x.PeriodTo).HasColumnType("date");
        b.Property(x => x.Prize).HasPrecision(14, 2).IsRequired();
    }
}

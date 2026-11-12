using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class GoLiveImportConfiguration : IEntityTypeConfiguration<GoLiveImport>
{
    public void Configure(EntityTypeBuilder<GoLiveImport> b)
    {
        b.ToTable("GoLiveImports");
        b.HasKey(x => x.Id);
        b.Property(x => x.SourceLockDate).HasColumnType("date");
        b.Property(x => x.SourceDigest).HasMaxLength(64);
        b.Property(x => x.SchemaVersion).HasMaxLength(40);
        b.Property(x => x.Profile).HasMaxLength(30);
        b.Property(x => x.Status).HasMaxLength(30);
        b.Property(x => x.TargetFingerprint).HasMaxLength(64);
        b.HasIndex(x => new { x.BranchId, x.SourceDigest }).IsUnique();
        b.HasIndex(x => x.BranchId).IsUnique().HasFilter("\"Status\" = 'Activated'");
        b.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

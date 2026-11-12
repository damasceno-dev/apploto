using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class GoLiveImportAuditConfiguration : IEntityTypeConfiguration<GoLiveImportAudit>
{
    public void Configure(EntityTypeBuilder<GoLiveImportAudit> b)
    {
        b.ToTable("GoLiveImportAudits"); b.HasKey(x => x.Id);
        b.Property(x => x.Action).HasMaxLength(30);
        b.HasOne<GoLiveImport>().WithMany().HasForeignKey(x => x.GoLiveImportId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal class GoLiveImportTargetConfiguration : IEntityTypeConfiguration<GoLiveImportTarget>
{
    public void Configure(EntityTypeBuilder<GoLiveImportTarget> b)
    {
        b.ToTable("GoLiveImportTargets"); b.HasKey(x => x.Id);
        b.Property(x => x.SourceTable).HasMaxLength(80); b.Property(x => x.SourceId).HasMaxLength(30); b.Property(x => x.TargetKind).HasMaxLength(30);
        b.HasIndex(x => new { x.BranchId, x.SourceTable, x.SourceId }).IsUnique();
        b.HasOne<GoLiveImport>().WithMany().HasForeignKey(x => x.GoLiveImportId).OnDelete(DeleteBehavior.Restrict);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class BankStatementImportConfiguration : IEntityTypeConfiguration<BankStatementImport>
{
    public void Configure(EntityTypeBuilder<BankStatementImport> b)
    {
        b.ToTable("BankStatementImports"); b.HasKey(e => e.Id);
        b.HasOne(e => e.AccountBinding).WithMany().HasForeignKey(e => e.AccountBindingId).OnDelete(DeleteBehavior.Restrict);
        b.Property(e => e.Fingerprint).HasMaxLength(64);
        b.Property(e => e.From).HasColumnType("date"); b.Property(e => e.To).HasColumnType("date");
        b.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Account>().WithMany().HasForeignKey(e => e.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => new { e.BranchId, e.BankAccountId, e.Fingerprint }).IsUnique();
    }
}
internal class BankStatementImportLineConfiguration : IEntityTypeConfiguration<BankStatementImportLine>
{
    public void Configure(EntityTypeBuilder<BankStatementImportLine> b)
    {
        b.ToTable("BankStatementImportLines"); b.HasKey(e => new { e.ImportId, e.LineId });
        b.HasOne(e => e.Import).WithMany(e => e.Lines).HasForeignKey(e => e.ImportId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Line).WithMany().HasForeignKey(e => e.LineId).OnDelete(DeleteBehavior.Restrict);
    }
}
internal class BankStatementLineConfiguration : IEntityTypeConfiguration<BankStatementLine>
{
    public void Configure(EntityTypeBuilder<BankStatementLine> b)
    {
        b.ToTable("BankStatementLines"); b.HasKey(e => e.Id); b.Property(e => e.Version).IsRowVersion();
        b.Property(e => e.BankIdentity).HasMaxLength(255); b.Property(e => e.Fingerprint).HasMaxLength(64);
        b.Property(e => e.Type).HasMaxLength(30); b.Property(e => e.Name).HasMaxLength(4096); b.Property(e => e.Memo).HasMaxLength(4096);
        b.Property(e => e.PostedOn).HasColumnType("date"); b.Property(e => e.Amount).HasColumnType("numeric(14,2)");
        b.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Account>().WithMany().HasForeignKey(e => e.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => new { e.BranchId, e.BankAccountId, e.BankIdentity }).IsUnique().HasFilter("\"BankIdentity\" IS NOT NULL");
    }
}
internal class BankMatchProposalConfiguration : IEntityTypeConfiguration<BankMatchProposal>
{
    public void Configure(EntityTypeBuilder<BankMatchProposal> b)
    {
        b.ToTable("BankMatchProposals"); b.HasKey(e => e.Id);
        b.Property(e => e.ExpectationFingerprint).HasMaxLength(64); b.Property(e => e.Amount).HasColumnType("numeric(14,2)");
        b.HasOne(e => e.Line).WithMany().HasForeignKey(e => e.LineId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Receipt).WithMany().HasForeignKey(e => e.ReceiptId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Account>().WithMany().HasForeignKey(e => e.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
internal class BankStatementMatchConfiguration : IEntityTypeConfiguration<BankStatementMatch>
{
    public void Configure(EntityTypeBuilder<BankStatementMatch> b)
    {
        b.ToTable("BankStatementMatches"); b.HasKey(e => e.Id);
        b.Property(e => e.Amount).HasColumnType("numeric(14,2)"); b.Property(e => e.PostedOn).HasColumnType("date");
        b.HasOne(e => e.Proposal).WithMany().HasForeignKey(e => e.ProposalId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<BankStatementLine>().WithMany().HasForeignKey(e => e.LineId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<BankReceipt>().WithMany().HasForeignKey(e => e.ReceiptId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => e.LineId).IsUnique(); b.HasIndex(e => e.ReceiptId).IsUnique(); b.HasIndex(e => e.ProposalId).IsUnique();
    }
}

internal class BankStatementAccountBindingConfiguration : IEntityTypeConfiguration<BankStatementAccountBinding>
{
    public void Configure(EntityTypeBuilder<BankStatementAccountBinding> b)
    {
        b.ToTable("BankStatementAccountBindings"); b.HasKey(e => e.Id); b.Property(e => e.Version).IsRowVersion();
        b.Property(e => e.BankIdentity).HasMaxLength(100); b.Property(e => e.AccountIdentity).HasMaxLength(100);
        b.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Account>().WithMany().HasForeignKey(e => e.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => e.BankAccountId).IsUnique();
        b.HasIndex(e => new { e.BranchId, e.BankIdentity, e.AccountIdentity }).IsUnique();
        b.ToTable(t => t.HasCheckConstraint("CK_BankStatementAccountBindings_Identity", "length(btrim(\"BankIdentity\")) > 0 AND length(btrim(\"AccountIdentity\")) > 0"));
    }
}

using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;

namespace server.Infrastructure;

public class ServerDbContext(DbContextOptions<ServerDbContext> options)
    : DbContext(options)
{
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
public DbSet<RecoveryThrottle> RecoveryThrottles => Set<RecoveryThrottle>();
    public DbSet<LoginThrottle> LoginThrottles => Set<LoginThrottle>();
public DbSet<OperatorLinkRequest> OperatorLinkRequests => Set<OperatorLinkRequest>();
public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<OperatedOnboarding> OperatedOnboardings => Set<OperatedOnboarding>();
    public DbSet<GoogleLoginChallenge> GoogleLoginChallenges => Set<GoogleLoginChallenge>();
    public DbSet<GoogleIdentity> GoogleIdentities => Set<GoogleIdentity>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<BranchUser> BranchUsers => Set<BranchUser>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<TransactionType> TransactionTypes => Set<TransactionType>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<OpeningDiscrepancyAdjustment> OpeningDiscrepancyAdjustments => Set<OpeningDiscrepancyAdjustment>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<Operator> Operators => Set<Operator>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<OperatorAccount> OperatorAccounts => Set<OperatorAccount>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<DailyClose> DailyCloses => Set<DailyClose>();
    public DbSet<DailyCloseItem> DailyCloseItems => Set<DailyCloseItem>();
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();
    public DbSet<TimeEntrySegment> TimeEntrySegments => Set<TimeEntrySegment>();
    public DbSet<TimeEntryPolicy> TimeEntryPolicies => Set<TimeEntryPolicy>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<IdempotencyRequest> IdempotencyRequests => Set<IdempotencyRequest>();
    public DbSet<DailyCloseReviewEvent> DailyCloseReviewEvents => Set<DailyCloseReviewEvent>();
    public DbSet<DailyCloseReviewEventItem> DailyCloseReviewEventItems => Set<DailyCloseReviewEventItem>();
    public DbSet<Terminal> Terminals => Set<Terminal>();
    public DbSet<SessionOpening> SessionOpenings => Set<SessionOpening>();
    public DbSet<SessionOpeningItem> SessionOpeningItems => Set<SessionOpeningItem>();
    public DbSet<SessionHandover> SessionHandovers => Set<SessionHandover>();
    public DbSet<SessionHandoverItem> SessionHandoverItems => Set<SessionHandoverItem>();
    public DbSet<SessionOpeningDiscrepancy> SessionOpeningDiscrepancies => Set<SessionOpeningDiscrepancy>();
    public DbSet<SessionOpeningDiscrepancyRevision> SessionOpeningDiscrepancyRevisions => Set<SessionOpeningDiscrepancyRevision>();
    public DbSet<SessionOpeningDiscrepancyRevisionItem> SessionOpeningDiscrepancyRevisionItems => Set<SessionOpeningDiscrepancyRevisionItem>();
    public DbSet<OpeningDiscrepancyAttributionEvent> OpeningDiscrepancyAttributionEvents => Set<OpeningDiscrepancyAttributionEvent>();
    public DbSet<SessionEvent> SessionEvents => Set<SessionEvent>();
    public DbSet<SessionCountAttempt> SessionCountAttempts => Set<SessionCountAttempt>();
    public DbSet<SessionCountAttemptItem> SessionCountAttemptItems => Set<SessionCountAttemptItem>();
    public DbSet<CustodyMappingIssue> CustodyMappingIssues => Set<CustodyMappingIssue>();
    public DbSet<OpeningCount> OpeningCounts => Set<OpeningCount>();
    public DbSet<OpeningCountItem> OpeningCountItems => Set<OpeningCountItem>();
    public DbSet<SessionFieldEffect> SessionFieldEffects => Set<SessionFieldEffect>();
    public DbSet<SessionFieldEffectRevision> SessionFieldEffectRevisions => Set<SessionFieldEffectRevision>();
    public DbSet<CustodyTransfer> CustodyTransfers => Set<CustodyTransfer>();
    public DbSet<SettlementEvent> SettlementEvents => Set<SettlementEvent>();
    public DbSet<BankStatementAccountBinding> BankStatementAccountBindings => Set<BankStatementAccountBinding>();
    public DbSet<BankStatementImport> BankStatementImports => Set<BankStatementImport>();
    public DbSet<BankStatementLine> BankStatementLines => Set<BankStatementLine>();
    public DbSet<BankMatchProposal> BankMatchProposals => Set<BankMatchProposal>();
    public DbSet<BankStatementMatch> BankStatementMatches => Set<BankStatementMatch>();
    public DbSet<BankMovement> BankMovements => Set<BankMovement>();
    public DbSet<CardProvider> CardProviders => Set<CardProvider>();
    public DbSet<CardSettlementFeeEvidence> CardSettlementFeeEvidence => Set<CardSettlementFeeEvidence>();
    public DbSet<CardSettlementBatch> CardSettlementBatches => Set<CardSettlementBatch>();
    public DbSet<BankReceipt> BankReceipts => Set<BankReceipt>();
    public DbSet<TransactionClassificationRevision> TransactionClassificationRevisions => Set<TransactionClassificationRevision>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ServerDbContext).Assembly);
    }
}

using Microsoft.EntityFrameworkCore;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;
using server.Domain.Entities;
namespace server.Infrastructure.Repositories;

internal class EmployeeStatementSources(ServerDbContext db) : IEmployeeStatementSources
{
    public async Task<IReadOnlyList<EmployeeSessionSource>> Sessions(
        Guid branchId,
        Guid operatorId,
        DateTime from,
        DateTime to,
        CancellationToken ct = default) =>
        await db.DailyCloses.AsNoTracking().Where(s => s.Active && s.BranchId == branchId && s.CustodianOperatorId == operatorId && s.Date >= from && s.Date <= to)
            .OrderBy(s => s.Date).ThenBy(s => s.Id).Take(10001)
            .Select(s => new EmployeeSessionSource(s.Id, s.AccountId, s.CustodianOperatorId, s.RecordedByOperatorId, s.SubmittedByOperatorId, s.Date, s.Status, s.SubmittedAttemptId, s.CustodyHistoryComplete)).ToListAsync(ct);

    public async Task<IReadOnlyList<EmployeeCommissionBasisSource>> CommissionBasis(
        Guid branchId,
        Guid operatorId,
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        var current = await db.Transactions.AsNoTracking().Where(t => t.Active && t.BranchId == branchId && t.Status == TransactionStatus.Active && t.Date >= from && t.Date <= to &&
            t.Session != null && !db.EmployeeCommissionFeePostings.Any(p => p.SessionId == t.SessionId) && t.Session.CustodianOperatorId == operatorId && t.Session.Active && t.Session.Status != DailyCloseStatus.Draft &&
            t.PostedFinancialClass == FinancialClass.ShopFeesAndIncome &&
            (t.PostedMonthlyFinancialRole == MonthlyFinancialRole.BolaoServiceFee || t.PostedMonthlyFinancialRole == MonthlyFinancialRole.SpecialDrawServiceFee))
            .OrderBy(t => t.Date).ThenBy(t => t.Id).Take(10001)
            .Select(t => new EmployeeCommissionBasisSource(t.Id, t.SessionId, t.Date, t.Date,
                t.PostedMonthlyFinancialRole == MonthlyFinancialRole.BolaoServiceFee ? CommissionBasisComponent.BolaoServiceFee : CommissionBasisComponent.SpecialDrawServiceFee,
                t.Direction == Direction.In ? t.Value : -t.Value, t.ClassificationProvenance, null, false)).ToListAsync(ct);
        var postings = await db.EmployeeCommissionFeePostings.AsNoTracking().Where(p => p.BranchId == branchId && p.OperatorId == operatorId && p.PostedOn >= from && p.PostedOn <= to)
            .OrderBy(p => p.PostedOn).ThenBy(p => p.Id).Take(10001)
            .Select(p => new EmployeeCommissionBasisSource(p.Id, p.SessionId, p.SourceDate, p.PostedOn, p.Component, p.SignedAmount, p.ClassificationProvenance, p.TransactionId, true)).ToListAsync(ct);
        return current.Concat(postings).OrderBy(p => p.PostedOn).ThenBy(p => p.SourceId).Take(10001).ToList();
    }

    public async Task<IReadOnlyList<EmployeeCommissionClassificationState>> CommissionClassificationStates(
        Guid branchId,
        Guid operatorId,
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        // Bound candidates by source window or a UTC envelope covering all business-zone offsets.
        // The caller applies its captured business clock to the exact classification dates.
        var utcFrom = DateTime.SpecifyKind(from.AddDays(-1), DateTimeKind.Utc);
        var utcUntil = DateTime.SpecifyKind(to.AddDays(2), DateTimeKind.Utc);
        // Compare the current explicit classification with the last official publication, without rewriting either.
        var rows = await db.Transactions.AsNoTracking().Where(t => t.BranchId == branchId && t.Session != null && t.Session.Active &&
            t.Session.CustodianOperatorId == operatorId && t.Session.Status != DailyCloseStatus.Draft && t.ClassifiedAt != null &&
            (t.Date >= from && t.Date <= to || t.ClassifiedAt >= utcFrom && t.ClassifiedAt < utcUntil) &&
            db.TransactionClassificationRevisions.Any(r => r.TransactionId == t.Id && r.BranchId == branchId &&
                (r.PreviousFinancialClass != r.FinancialClass || r.PreviousMonthlyFinancialRole != r.MonthlyFinancialRole) &&
                (r.PreviousFinancialClass == FinancialClass.ShopFeesAndIncome && (r.PreviousMonthlyFinancialRole == MonthlyFinancialRole.BolaoServiceFee || r.PreviousMonthlyFinancialRole == MonthlyFinancialRole.SpecialDrawServiceFee) ||
                 r.FinancialClass == FinancialClass.ShopFeesAndIncome && (r.MonthlyFinancialRole == MonthlyFinancialRole.BolaoServiceFee || r.MonthlyFinancialRole == MonthlyFinancialRole.SpecialDrawServiceFee))))
            .OrderBy(t => t.Id).Take(10001).Select(t => new
            {
                t.Id,
                t.SessionId,
                t.Date,
                t.ClassifiedAt,
                t.Active,
                t.Status,
                t.Value,
                t.Direction,
                t.PostedFinancialClass,
                t.PostedMonthlyFinancialRole,
                t.Session!.SubmittedAt,
                HasPublication = db.EmployeeCommissionFeePostings.Any(p => p.TransactionId == t.Id),
                Bolao = db.EmployeeCommissionFeePostings.Where(p => p.TransactionId == t.Id && p.Component == CommissionBasisComponent.BolaoServiceFee).Sum(p => (decimal?)p.SignedAmount) ?? 0,
                Special = db.EmployeeCommissionFeePostings.Where(p => p.TransactionId == t.Id && p.Component == CommissionBasisComponent.SpecialDrawServiceFee).Sum(p => (decimal?)p.SignedAmount) ?? 0
            }).ToListAsync(ct);
        return rows.Select(t =>
        {
            var amount = t.Active && t.Status == TransactionStatus.Active && t.PostedFinancialClass == FinancialClass.ShopFeesAndIncome
                ? t.Value * (t.Direction == Direction.In ? 1 : -1) : 0;
            var bolao = t.PostedMonthlyFinancialRole == MonthlyFinancialRole.BolaoServiceFee ? amount : 0;
            var special = t.PostedMonthlyFinancialRole == MonthlyFinancialRole.SpecialDrawServiceFee ? amount : 0;
            var matches = t.Bolao == bolao && t.Special == special && (t.HasPublication || t.SubmittedAt > t.ClassifiedAt);
            return new EmployeeCommissionClassificationState(t.Id, t.SessionId!.Value, t.Date, t.ClassifiedAt!.Value, matches);
        }).ToList();
    }

    public async Task CaptureCommissionBasis(Guid branchId, Guid sessionId, DateTime postedOn, DateTime recordedAt, CancellationToken ct = default)
    {
        var session = await db.DailyCloses.SingleAsync(s => s.Id == sessionId && s.BranchId == branchId, ct);
        if (session.CustodianOperatorId == null || !session.CustodyHistoryComplete || session.SubmittedAttemptId == null) return;
        var previous = await db.EmployeeCommissionFeePostings.AsNoTracking().Where(p => p.SessionId == sessionId).ToListAsync(ct);
        var transactions = await db.Transactions.AsNoTracking().Where(t => t.SessionId == sessionId && t.BranchId == branchId).ToListAsync(ct);
        foreach (var transaction in transactions)
        {
            CommissionBasisComponent? currentComponent = transaction.PostedFinancialClass == FinancialClass.ShopFeesAndIncome
                ? transaction.PostedMonthlyFinancialRole switch
                {
                    MonthlyFinancialRole.BolaoServiceFee => CommissionBasisComponent.BolaoServiceFee,
                    MonthlyFinancialRole.SpecialDrawServiceFee => CommissionBasisComponent.SpecialDrawServiceFee,
                    _ => null
                } : null;
            var keys = previous.Where(p => p.TransactionId == transaction.Id).Select(p => (p.Component, p.SourceDate)).ToHashSet();
            if (currentComponent is { } component) keys.Add((component, transaction.Date));
            foreach (var key in keys)
            {
                var prior = previous.Where(p => p.TransactionId == transaction.Id && p.Component == key.Component && p.SourceDate == key.SourceDate).ToList();
                var currentAmount = transaction.Active && transaction.Status == TransactionStatus.Active && currentComponent == key.Component && transaction.Date == key.SourceDate
                    ? transaction.Value * (transaction.Direction == Direction.In ? 1 : -1) : 0;
                var delta = currentAmount - prior.Sum(p => p.SignedAmount);
                if (delta == 0) continue;
                db.EmployeeCommissionFeePostings.Add(new EmployeeCommissionFeePosting
                {
                    BranchId = branchId,
                    OperatorId = session.CustodianOperatorId.Value,
                    SessionId = sessionId,
                    AttemptId = session.SubmittedAttemptId.Value,
                    TransactionId = transaction.Id,
                    SourceDate = key.SourceDate,
                    PostedOn = postedOn,
                    CreatedAt = recordedAt,
                    Component = key.Component,
                    SignedAmount = delta,
                    ClassificationRevision = transaction.ClassificationRevision,
                    ClassificationProvenance = currentAmount == 0 ? prior.OrderBy(p => p.PostedOn).ThenBy(p => p.CreatedAt).Last().ClassificationProvenance : transaction.ClassificationProvenance
                });
            }
        }
    }


    public async Task<IReadOnlyList<EmployeeSessionSource>> UnresolvedCustodySessions(Guid branchId, DateTime from, DateTime to, CancellationToken ct = default) =>
        await db.DailyCloses.AsNoTracking().Where(s => s.Active && s.BranchId == branchId && s.Date >= from && s.Date <= to &&
            (s.MappingState != SessionMappingState.Ready || s.CustodianOperatorId == null || !s.CustodyHistoryComplete))
            .OrderBy(s => s.Date).ThenBy(s => s.Id).Take(10001)
            .Select(s => new EmployeeSessionSource(s.Id, s.AccountId, s.CustodianOperatorId, s.RecordedByOperatorId, s.SubmittedByOperatorId, s.Date, s.Status, s.SubmittedAttemptId, s.CustodyHistoryComplete)).ToListAsync(ct);

    public async Task LockCommissionAccounts(Guid branchId, CancellationToken ct = default)
    {
        var accounts = await db.Accounts.Where(a => a.BranchId == branchId && a.Type == AccountType.Terminal).Select(a => a.Id).ToListAsync(ct);
        foreach (var key in accounts.Select(a => Services.DailyCloseAccountCoordinationKey.Compute(branchId, a)).Distinct().Order())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", ct);
    }

    public async Task LockSpecialFee(Guid branchId, Guid effectId, CancellationToken ct = default)
    {
        var account = await db.SessionFieldEffects.Where(e => e.Id == effectId && e.BranchId == branchId).Select(e => (Guid?)e.Session.AccountId).SingleOrDefaultAsync(ct);
        if (account != null)
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({Services.DailyCloseAccountCoordinationKey.Compute(branchId, account.Value)})", ct);
    }
    public async Task<IReadOnlyList<SpecialFeeSource>> SpecialFees(
        Guid branchId,
        Guid? operatorId,
        DateTime from,
        DateTime to,
        CancellationToken ct = default,
        IReadOnlyCollection<Guid>? includeEffects = null,
        IReadOnlyCollection<Guid>? includeSessions = null) =>
        await db.SessionFieldEffects.AsNoTracking().Where(e => e.BranchId == branchId && e.Field == SessionCloseField.SpecialFee && e.Session.Active &&
            (e.Session.Date >= from && e.Session.Date <= to || includeEffects != null && includeEffects.Contains(e.Id) || includeSessions != null && includeSessions.Contains(e.SessionId)) &&
            (operatorId == null || e.Session.CustodianOperatorId == operatorId))
            .OrderBy(e => e.Session.Date).ThenBy(e => e.Id).Take(10001)
            .Select(e => new SpecialFeeSource(e.Id, e.SessionId, e.BranchId, e.Session.Date, e.Revision, e.Value, e.CurrentTransactionId,
                e.Session.Status != DailyCloseStatus.Draft && (e.Value == 0 || e.CurrentTransaction != null && e.CurrentTransaction.Active && e.CurrentTransaction.Status == TransactionStatus.Active &&
                e.CurrentTransaction.PostedMonthlyFinancialRole == MonthlyFinancialRole.SpecialDrawServiceFee && e.CurrentTransaction.PostedFinancialClass == FinancialClass.ShopFeesAndIncome),
                e.Revisions.Where(r => r.CreatedTransactionId != null).Select(r => r.CreatedTransactionId!.Value).ToList())).ToListAsync(ct);

    public async Task<SpecialFeeSource?> SpecialFee(Guid branchId, Guid effectId, CancellationToken ct = default) =>
        await db.SessionFieldEffects.AsNoTracking().Where(e => e.BranchId == branchId && e.Id == effectId && e.Field == SessionCloseField.SpecialFee && e.Session.Active)
            .Select(e => new SpecialFeeSource(e.Id, e.SessionId, e.BranchId, e.Session.Date, e.Revision, e.Value, e.CurrentTransactionId,
                e.Session.Status != DailyCloseStatus.Draft && e.CurrentTransaction != null && e.CurrentTransaction.Active && e.CurrentTransaction.Status == TransactionStatus.Active &&
                e.CurrentTransaction.PostedMonthlyFinancialRole == MonthlyFinancialRole.SpecialDrawServiceFee && e.CurrentTransaction.PostedFinancialClass == FinancialClass.ShopFeesAndIncome, null)).SingleOrDefaultAsync(ct);
}

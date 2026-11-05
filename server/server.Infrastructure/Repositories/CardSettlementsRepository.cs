using Microsoft.EntityFrameworkCore;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class CardSettlementsRepository(ServerDbContext db) : ICardSettlementsRepository
{
    public async Task<IReadOnlyList<CardProvider>> ListProvidersAsNoTracking(Guid branchId, CancellationToken ct = default) =>
        await db.CardProviders.AsNoTracking().Where(p => p.BranchId == branchId && p.Active).OrderBy(p => p.Name).ThenBy(p => p.Id).ToListAsync(ct);
    public async Task AddProvider(CardProvider provider, CancellationToken ct = default) => await db.CardProviders.AddAsync(provider, ct);
    public Task<CardProvider?> GetProviderAsNoTracking(Guid id, Guid branchId, CancellationToken ct = default) =>
        db.CardProviders.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id && p.BranchId == branchId && p.Active, ct);
    public async Task LockProviderIdentity(Guid branchId, string identity, CancellationToken ct = default)
    {
        var scope = "loto:card-provider:" + JsonSerializer.Serialize(new[] { branchId.ToString("N"), identity });
        var key = BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(scope)));
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", ct);
    }
    public Task<bool> ProviderIdentityExists(Guid branchId, string identity, CancellationToken ct = default) =>
        db.CardProviders.AnyAsync(p => p.BranchId == branchId && p.ExternalIdentity == identity, ct);
    public Task<CardSettlementBatch?> GetBatchAsNoTracking(Guid id, Guid branchId, CancellationToken ct = default) =>
        db.CardSettlementBatches.AsNoTracking().SingleOrDefaultAsync(b => b.Id == id && b.BranchId == branchId, ct);
    public async Task AddFeeEvidence(CardSettlementFeeEvidence evidence, CancellationToken ct = default) => await db.CardSettlementFeeEvidence.AddAsync(evidence, ct);
    public async Task AddBatch(CardSettlementBatch batch, CancellationToken ct = default) => await db.CardSettlementBatches.AddAsync(batch, ct);
    public Task<bool> BatchIdentityExists(Guid providerId, string identity, CancellationToken ct = default) =>
        db.CardSettlementBatches.AnyAsync(p => p.ProviderId == providerId && p.ExternalIdentity == identity, ct);
}

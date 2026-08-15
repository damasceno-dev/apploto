namespace server.Application.Services.Settings;

public interface ILockDateReader
{
    Task<DateTime> Read(Guid branchId, CancellationToken ct = default);
}

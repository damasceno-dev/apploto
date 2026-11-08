using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
namespace server.Application.UseCases.People;

public class PeopleRecordReader(IAuthenticationService authentication, IEmployeeRecordsRepository records)
{
    public async Task<IReadOnlyList<T>> List<T>(CancellationToken ct = default) where T : EntityBase
    {
        var actor = await authentication.GetAuthenticatedBranchUser();
        if (actor.Role is not Role.Manager and not Role.Admin) throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
        var rows = await records.List<T>(actor.BranchId, ct);
        if (rows.Count > 10000) throw new ConflictException(ResourcesErrorMessages.PEOPLE_REPORT_TOO_LARGE);
        return rows;
    }
}

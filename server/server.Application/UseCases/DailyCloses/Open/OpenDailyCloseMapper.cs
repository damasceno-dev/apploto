using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;

namespace server.Application.UseCases.DailyCloses.Open;

public static class OpenDailyCloseMapper
{
    /// <summary>
    /// Builds a new <see cref="DailyClose"/> entity from the request. Navigation properties
    /// are intentionally left unset so EF Core does not try to re-insert already-persisted
    /// related entities when the close is added to the DbContext.
    /// </summary>
    public static DailyClose ToDomain(
        this RequestOpenDailyCloseJson request,
        Guid branchId,
        Guid openedByUserId)
    {
        return new DailyClose
        {
            Date = request.Date,
            Status = DailyCloseStatus.Draft,
            AccountId = request.AccountId,
            BranchId = branchId,
            OpenedByUserId = openedByUserId
        };
    }
}

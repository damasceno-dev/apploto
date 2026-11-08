using server.Application.Services.Transactions;
using server.Application.Services.Reports;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
namespace server.Application.UseCases.People;

public class RecordPayItemUseCase(
    PeopleCommandRunner runner,
    IEmployeeRecordsRepository records,
    IUnitOfWork unitOfWork,
    IBranchClock clock,
    EmployeeStatementReader statements,
    IEmployeeStatementSources sources)
{
    public Task<ResponsePayItemJson> Execute(RequestPayItemJson request, string? key, uint? version = null, CancellationToken ct = default)
    {
        PeopleValidation.Validate(new PayItemFluentValidation(), request);
        return runner.Run<RequestPayItemJson, ResponsePayItemJson>(PeopleCommandRunner.Endpoint("pay-item", request.PreviousId), request, key, version, request.OperatorId,
            async (actor, token) =>
            {
                if (request.CommissionCalculationId != null) await sources.LockCommissionAccounts(actor.BranchId, token);
                var rows = await records.List<EmployeePayItem>(actor.BranchId, token);
                PeopleHistory.SourceExists(rows, request.PreviousId);
                PeopleHistory.SourceExists(await records.List<EmployeePayCategory>(actor.BranchId, token), request.CategoryId);
            },
            async (actor, locked, token) =>
            {
                var rows = await records.List<EmployeePayItem>(actor.BranchId, token);
                var prior = PeopleHistory.Previous(rows, request.PreviousId, version, r => r.PreviousId, r => r.Version);
                var categories = await records.List<EmployeePayCategory>(actor.BranchId, token);
                var category = (prior != null ? categories : PeopleHistory.Heads(categories, r => r.PreviousId)).SingleOrDefault(r => r.Id == request.CategoryId && (prior != null || r.Enabled));
                PeopleHistory.Require(category != null && category.Kind == request.Kind);
                PeopleHistory.Require(request.SourceDate <= request.PostedOn && request.SourceDate.Year == request.SourceMonth.Year && request.SourceDate.Month == request.SourceMonth.Month);
                if (prior == null) { PeopleHistory.Open(request.SourceMonth, locked); PeopleHistory.Require(!request.Cancelled); }
                else PeopleHistory.Require(prior.OperatorId == request.OperatorId && prior.SourceMonth == request.SourceMonth && prior.SourceDate == request.SourceDate && prior.CategoryId == request.CategoryId && prior.Kind == request.Kind && (prior.CommissionCalculationId != null) == (request.CommissionCalculationId != null) && !prior.Cancelled && request.PostedOn >= prior.PostedOn);
                if (request.Kind == PayItemKind.Deduction && string.IsNullOrWhiteSpace(request.DeductionPolicy))
                    throw new OnValidationException([ResourcesErrorMessages.PEOPLE_POLICY_REQUIRED]);
                string? commissionEvidence = prior?.CommissionEvidenceJson;
                if (request.CommissionCalculationId != null)
                {
                    PeopleHistory.Require(request.Kind == PayItemKind.Addition);
                    if (!request.Cancelled)
                    {
                        var calculation = await statements.ReadForCommand(actor.BranchId, request.OperatorId, request.SourceMonth.Year, request.SourceMonth.Month, request.PostedOn, token);
                        commissionEvidence = System.Text.Json.JsonSerializer.Serialize(calculation.Commission);
                        PeopleHistory.Require(calculation.Commission.CalculationId == request.CommissionCalculationId && calculation.Commission.ProposedAmount == request.Amount);
                        if (prior == null && rows.Any(r => r.CommissionCalculationId != null && r.OperatorId == request.OperatorId && r.SourceMonth == request.SourceMonth)) throw new ConflictException(ResourcesErrorMessages.PEOPLE_DUPLICATE_SOURCE);
                    }
                    else PeopleHistory.Require(prior != null && request.CommissionCalculationId == prior.CommissionCalculationId && request.Amount == prior.Amount);
                }
                var target = request.Cancelled ? 0 : request.Amount * (request.Kind == PayItemKind.Addition ? 1 : -1);
                var oldTarget = prior == null || prior.Cancelled ? 0 : prior.Amount * (prior.Kind == PayItemKind.Addition ? 1 : -1);
                var row = new EmployeePayItem
                {
                    BranchId = actor.BranchId, ActorUserId = actor.UserId, CreatedAt = clock.UtcNow(), PostedOn = request.PostedOn,
                    Reason = request.Reason.Trim(), Evidence = request.Evidence.Trim(), PreviousId = request.PreviousId,
                    OperatorId = request.OperatorId, CategoryId = request.CategoryId, CategoryName = category!.Name, SourceMonth = request.SourceMonth,
                    SourceDate = request.SourceDate, Amount = request.Amount, Kind = request.Kind, DeductionPolicy = request.DeductionPolicy?.Trim(),
                    CommissionEvidenceJson = commissionEvidence, Cancelled = request.Cancelled, SignedContribution = target - oldTarget, CommissionCalculationId = request.CommissionCalculationId,
                };
                await records.Add(row, token);
                await unitOfWork.Commit(token);
                return (row.Id, row.ToResponse());
            }, ct);
    }
}

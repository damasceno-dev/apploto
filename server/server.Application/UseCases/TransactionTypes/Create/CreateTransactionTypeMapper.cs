using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;

namespace server.Application.UseCases.TransactionTypes.Create;

internal static class CreateTransactionTypeMapper
{
    public static TransactionType ToDomain(this RequestCreateTransactionTypeJson request)
    {
        return new TransactionType
        {
            Name = request.Name.Trim(),
            SettlementRule = request.SettlementRule,
            RequiresTabAccountAndClient = request.RequiresTabAccountAndClient,
            FiadoPaymentKind = request.FiadoPaymentKind,
            SettlementInstrument = request.SettlementInstrument ?? SettlementInstrument.None,
            MonthlyFinancialRole = request.MonthlyFinancialRole ?? MonthlyFinancialRole.None,
            CategoryId = request.CategoryId
        };
    }
}

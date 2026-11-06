using System.Text;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;

namespace WebApi.Test.Infrastructure;

/// <summary>Supported synthetic card → receipt → OFX path, without provider access or bank-ledger compensation.</summary>
internal static class PostedCardJourney
{
    public static async Task ConfirmAsync(
        ServerWebApplicationFactory factory,
        HttpClient client,
        Guid branchId,
        string managerToken,
        Transaction claim)
    {
        var bank = await factory.SeedAccountAsync(branchId, AccountType.BankAccount, "Banco sintético da demonstração");
        var provider = await Post<ResponseCardProviderJson>("/card-provider", new RequestCardProviderJson
        {
            Name = "Provedor sintético", ExternalIdentity = "responsibility-demo", BankAccountId = bank.Id,
            Brand = "Teste", DelayDays = 0, BusinessDays = false
        });
        var date = new DateTime(2026, 10, 2);
        var batch = await Post<ResponseCardSettlementJson>("/card-settlement", new RequestCardSettlementJson
        {
            ProviderId = provider.Id, ProviderVersion = provider.Version, ExternalIdentity = "demo-batch",
            OccurredOn = date, PostedOn = date, PlanningBasisOn = date,
            ConfirmedFee = 2m, ManagerConfirmedFee = true, FeeBasis = "Valor confirmado", FeeEvidence = "Evidência sintética",
            ObservedNetAmount = 98m,
            Items = [new() { TransactionId = claim.Id, TransactionVersion = claim.Version, ExpectedVersion = 0, GrossAmount = 100m }]
        });
        var binding = await Post<ResponseBankStatementAccountBindingJson>("/bank-statement/account-binding",
            new RequestBankStatementAccountBindingJson { BankAccountId = bank.Id, BankIdentity = "104", AccountIdentity = "responsibility-demo" });
        const string ofx = "<?xml version=\"1.0\"?><OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS><CURDEF>BRL</CURDEF><BANKACCTFROM><BANKID>104</BANKID><ACCTID>responsibility-demo</ACCTID></BANKACCTFROM><BANKTRANLIST><DTSTART>20261001</DTSTART><DTEND>20261005</DTEND><STMTTRN><TRNTYPE>CREDIT</TRNTYPE><DTPOSTED>20261002</DTPOSTED><TRNAMT>98.00</TRNAMT><FITID>demo-credit</FITID></STMTTRN></BANKTRANLIST></STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>";
        var import = await Post<ResponseBankStatementJson>("/bank-statement/import", new RequestBankStatementImportJson
        {
            AccountBindingId = binding.Id, BankAccountId = bank.Id, BankIdentity = "104", AccountIdentity = "responsibility-demo",
            ContentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(ofx))
        });
        var candidates = await Post<ResponseBankMatchProposalsJson>("/bank-statement/propose", new RequestBankMatchProposeJson { ImportId = import.Id });
        var selected = candidates.Proposals.ShouldHaveSingleItem();
        selected.ReceiptId.ShouldBe(batch.BankReceipt.Id);
        await Post<ResponseBankMatchConfirmationJson>("/bank-statement/confirm", new RequestBankMatchConfirmJson
        {
            PostedOn = date, Items = [new()
            {
                ProposalId = selected.Id, LineId = selected.LineId, LineVersion = selected.LineVersion,
                ReceiptId = selected.ReceiptId, ReceiptVersion = selected.ReceiptVersion,
                Amount = selected.Amount, ExpectationFingerprint = selected.ExpectationFingerprint
            }]
        });

        async Task<T> Post<T>(string route, object request) where T : class
        {
            var response = await client.PostAuthAsync(route, request, managerToken, Guid.NewGuid().ToString());
            response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());
            return await response.ReadContentAsync<T>();
        }
    }
}

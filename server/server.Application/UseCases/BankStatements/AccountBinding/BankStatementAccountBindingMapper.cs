using server.Domain.Entities;
using server.Communication.Responses;

namespace server.Application.UseCases.BankStatements.AccountBinding;

internal static class BankStatementAccountBindingMapper
{
    extension(BankStatementAccountBinding binding)
    {
        public ResponseBankStatementAccountBindingJson ToResponse() => new()
        {
            Id = binding.Id, Version = binding.Version, BankAccountId = binding.BankAccountId,
            BankIdentity = binding.BankIdentity, AccountIdentity = binding.AccountIdentity,
            CreatedByUserId = binding.CreatedByUserId, CreatedAt = binding.CreatedAt
        };
    }
}

using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.UseCases.CardProviders;

internal static class CardProviderMapper
{
    extension(CardProvider provider)
    {
        public ResponseCardProviderJson ToResponse() => new()
        {
            Id = provider.Id,
            Version = provider.Version,
            Name = provider.Name,
            ExternalIdentity = provider.ExternalIdentity,
            BankAccountId = provider.BankAccountId,
            Brand = provider.Brand,
            DelayDays = provider.DelayDays,
            BusinessDays = provider.BusinessDays,
            Instrument = provider.Instrument
        };
    }
}

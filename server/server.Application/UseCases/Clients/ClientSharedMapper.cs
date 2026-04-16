using System.Text.RegularExpressions;
using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.UseCases.Clients;

public static partial class ClientSharedMapper
{
    extension(Client client)
    {
        public ResponseClientJson ToClientResponse()
        {
            return new ResponseClientJson
            {
                Id = client.Id,
                Name = client.Name,
                Phone = client.Phone,
                Cpf = client.Cpf,
                Cep = client.Cep,
                Address = client.Address,
                PhoneSecondary = client.PhoneSecondary,
                Notes = client.Notes,
                Email = client.Email,
                BranchId = client.BranchId
            };
        }
    }

    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigitCharacter();
    
    // Strips all non-digit characters; returns null for null or whitespace-only input.
    internal static string? NormalizeCpf(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : NonDigitCharacter().Replace(value, "");

    // Returns null for null or whitespace-only input; trims otherwise.
    internal static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

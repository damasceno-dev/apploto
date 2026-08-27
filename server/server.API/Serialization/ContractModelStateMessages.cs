using Microsoft.AspNetCore.Mvc.ModelBinding;
using server.Exceptions;

namespace server.Serialization;

internal static class ContractModelStateMessages
{
    private static readonly HashSet<string> BackendAuthored = new(StringComparer.Ordinal)
    {
        ResourcesErrorMessages.ENUM_NAME_INVALID,
        ResourcesErrorMessages.REQUEST_INVALID
    };

    internal static List<string> Describe(ModelStateDictionary modelState)
    {
        var messages = modelState.Values
            .SelectMany(entry => entry.Errors)
            .Select(GetBackendAuthoredMessage)
            .Where(static message => message is not null)
            .Select(static message => message!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return messages.Count is 0 ? [ResourcesErrorMessages.REQUEST_INVALID] : messages;
    }

    private static string? GetBackendAuthoredMessage(ModelError error)
    {
        for (var exception = error.Exception; exception is not null; exception = exception.InnerException)
        {
            if (exception is ContractJsonException contractException &&
                BackendAuthored.Contains(contractException.ClientMessage))
                return contractException.ClientMessage;
        }

        return BackendAuthored.Contains(error.ErrorMessage) ? error.ErrorMessage : null;
    }
}

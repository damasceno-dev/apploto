using System.Text.Json;

namespace server.Serialization;

internal sealed class ContractJsonException(string clientMessage) : JsonException(clientMessage)
{
    internal string ClientMessage { get; } = clientMessage;
}

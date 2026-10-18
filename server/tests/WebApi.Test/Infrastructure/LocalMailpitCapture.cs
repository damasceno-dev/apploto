using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace WebApi.Test.Infrastructure;

internal sealed class LocalMailpitCapture : IAsyncDisposable
{
    private readonly IContainer _container = new ContainerBuilder("axllent/mailpit:v1.31.5")
        .WithPortBinding(1025, true).WithPortBinding(8025, true)
        .WithEnvironment("MP_DATABASE", "/dev/shm/mailpit.db")
        .WithCreateParameterModifier(parameters =>
        {
            foreach (var bindings in parameters.HostConfig!.PortBindings!.Values)
                foreach (var binding in bindings) binding.HostIP = "127.0.0.1";
        })
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(1025)).Build();

    public Task StartAsync() => _container.StartAsync();
    public ushort GetMappedPublicPort(int port) => _container.GetMappedPublicPort(port);
    public ValueTask DisposeAsync() => _container.DisposeAsync();

    public async Task<string> WaitToken(string email)
    {
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{GetMappedPublicPort(8025)}") };
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            using var messages = JsonDocument.Parse(await http.GetStringAsync("/api/v1/messages"));
            var message = messages.RootElement.GetProperty("messages").EnumerateArray()
                .FirstOrDefault(m => m.GetProperty("To").EnumerateArray().Any(to => to.GetProperty("Address").GetString() == email));
            if (message.ValueKind != JsonValueKind.Undefined)
            {
                using var details = JsonDocument.Parse(await http.GetStringAsync("/api/v1/message/" + message.GetProperty("ID").GetString()));
                return Uri.UnescapeDataString(details.RootElement.GetProperty("Text").GetString()!.Split("#token=", StringSplitOptions.None)[1].Trim());
            }
            await Task.Delay(25);
        }
        throw new TimeoutException("Expected local Mailpit capability message was not captured.");
    }
}

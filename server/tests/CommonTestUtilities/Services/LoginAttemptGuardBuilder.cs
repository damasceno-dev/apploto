using CommonTestUtilities.Repositories;
using NSubstitute;
using server.Application.Services.Security;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Services;

/// <summary>Builds the real <see cref="LoginAttemptGuard"/> over a store-like repository substitute.</summary>
public sealed class LoginAttemptGuardBuilder
{
    public const string DefaultNetwork = "v4:203.0.113.10";

    private TimeProvider _time = new FixedUtcTimeProvider(new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero));
    private LoginProtectionOptions _options = LoginProtectionOptionsFactory.Create(null, null, null, null, null);
    private readonly IClientNetworkProvider _client = Substitute.For<IClientNetworkProvider>();

    public LoginAttemptGuardBuilder() => _client.Current().Returns(DefaultNetwork);

    public LoginThrottleRepositoryBuilder Throttles { get; } = new();

    public IClientNetworkProvider Client => _client;

    public LoginAttemptGuardBuilder WithTime(TimeProvider time)
    {
        _time = time;
        return this;
    }

    public LoginAttemptGuardBuilder WithOptions(LoginProtectionOptions options)
    {
        _options = options;
        return this;
    }

    public LoginAttemptGuardBuilder FromNetwork(string network)
    {
        _client.Current().Returns(network);
        return this;
    }

    public LoginAttemptGuard Build() => new(Throttles.Build(), _client, _options, _time);
}

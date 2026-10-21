using NSubstitute;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Repositories;

/// <summary>
/// A substitute that behaves like a store: <see cref="Rows"/> is what a later <c>GetForUpdate</c> returns, so a test
/// can drive several attempts in sequence and still assert <c>Received()</c> calls.
/// </summary>
public class LoginThrottleRepositoryBuilder
{
    private readonly ILoginThrottleRepository _repository = Substitute.For<ILoginThrottleRepository>();

    public List<LoginThrottle> Rows { get; } = [];

    public LoginThrottleRepositoryBuilder()
    {
        _repository.GetForUpdate(Arg.Any<string[]>())
            .Returns(call => Rows.Where(r => call.Arg<string[]>().Contains(r.KeyHash)).ToList());
        _repository.Add(Arg.Any<LoginThrottle>()).Returns(Task.CompletedTask)
            .AndDoes(call => Rows.Add(call.Arg<LoginThrottle>()));
        _repository.When(r => r.Remove(Arg.Any<LoginThrottle>())).Do(call => Rows.Remove(call.Arg<LoginThrottle>()));
        _repository.RemoveAccount(Arg.Any<string>()).Returns(Task.CompletedTask)
            .AndDoes(call => Rows.RemoveAll(r => r.AccountKeyHash == call.Arg<string>()));
    }

    public LoginThrottleRepositoryBuilder WithRow(LoginThrottle row)
    {
        Rows.Add(row);
        return this;
    }

    public ILoginThrottleRepository Build() => _repository;
}

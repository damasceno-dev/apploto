using NSubstitute;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Services;

public sealed class SecurityCoordinationBuilder
{
    public static ISecurityCoordination Build(ISecurityScope? scope = null)
    {
        var coordination = Substitute.For<ISecurityCoordination>();
        coordination.TryAcquire(Arg.Any<string[]>()).Returns(scope ?? Substitute.For<ISecurityScope>());
        return coordination;
    }
}

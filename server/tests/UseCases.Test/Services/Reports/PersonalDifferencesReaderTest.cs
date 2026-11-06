using NSubstitute;
using server.Application.Services.Reports;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Domain.Models.Projections;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.Reports;

public class PersonalDifferencesReaderTest
{
    [Theory]
    [InlineData(null, false)]
    [InlineData(19, true)]
    [InlineData(20, true)]
    [InlineData(21, false)]
    public async Task Threshold_UsesGrossShortagesAndNeverUnassignedOrPayroll(int? threshold, bool alert)
    {
        var branch = Guid.NewGuid(); var product = Guid.NewGuid(); var person = Guid.NewGuid();
        var from = new DateTime(2026, 10, 1); var to = new DateTime(2026, 10, 5);
        var repository = Substitute.For<IPersonalDifferencesRepository>();
        repository.Read(branch, product, from, to, null, 1, 50, Arg.Any<CancellationToken>()).Returns(
            new PersonalDifferencePage([
                new(person, "Ana", false, 20m, 100m, 3, 20m),
                new(null, null, null, 999m, 0m, 1, 0m)], 4, []));
        var settings = Substitute.For<ISettingsRepository>();
        settings.GetByBranchIdAsNoTracking(branch, Arg.Any<CancellationToken>()).Returns(new Setting { PersonalShortageThreshold = threshold });
        var response = await new PersonalDifferencesReader(repository, settings).Read(branch, product, 2026, 10, to);
        var actual = response.People.Single(p => p.OperatorId == person);
        (actual.Shortage, actual.Overage, actual.SignedNet, actual.CurrentThresholdAlert).ShouldBe((20m, 100m, 80m, alert));
        response.People.ShouldNotContain(p => p.OperatorId == null);
        response.UnassignedSourceCount.ShouldBe(1);
        response.AutomaticPayrollDeduction.ShouldBeFalse();
        await repository.Received(1).Read(branch, product, from, to, null, 1, 50, Arg.Any<CancellationToken>());
    }
}

using CommonTestUtilities.Entities;
using server.Application.UseCases.Transactions.EditPreview;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Transactions.EditPreview;

/// <summary>
/// The overlay value object. Pure construction, no mocks. Pins that immutable attributes
/// come from the snapshot, every editable field exposes the snapshot value as
/// <c>Current*</c> and the payload value as <c>Hypothetical*</c>, and the snapshot itself is
/// never mutated. Includes <c>Description</c> and <c>TransactionTime</c> to maintain parity with the update request,
/// despite them having no financial impact on reports..
/// </summary>
public class HypotheticalTransactionEditTest
{
    [Fact]
    public void Constructor_ShouldOverlayPayloadOntoSnapshot_AndLeaveSnapshotUnmutated()
    {
        var snapshotClientId = Guid.NewGuid();
        var payloadClientId = Guid.NewGuid();
        var snapshot = BuildSnapshot(
            direction: Direction.Out,
            value: 250m,
            date: new DateTime(2025, 3, 10),
            status: TransactionStatus.Active,
            clientId: snapshotClientId,
            dueDate: new DateTime(2025, 3, 20),
            paidAt: new DateTime(2025, 3, 15),
            transactionTime: new TimeOnly(8, 0),
            description: "current desc");

        var payload = new RequestUpdateTransactionJson
        {
            Description = "new desc",
            DueDate = new DateTime(2025, 4, 1),
            PaidAt = new DateTime(2025, 3, 25),
            ClientId = payloadClientId,
            TransactionTime = new TimeOnly(17, 45)
        };

        var edit = new HypotheticalTransactionEdit(snapshot, payload);

        // Immutable attributes — straight off the snapshot.
        edit.TransactionId.ShouldBe(snapshot.Id);
        edit.BranchId.ShouldBe(snapshot.BranchId);
        edit.AccountId.ShouldBe(snapshot.AccountId);
        edit.AccountType.ShouldBe(AccountType.Tab);
        edit.Date.ShouldBe(new DateTime(2025, 3, 10));

        // Value/Direction are single-source today (payload can't change them), so both sides of
        // each pair equal the snapshot.
        edit.CurrentValue.ShouldBe(250m);
        edit.HypotheticalValue.ShouldBe(250m);
        edit.CurrentDirection.ShouldBe(Direction.Out);
        edit.HypotheticalDirection.ShouldBe(Direction.Out);

        // Current* = snapshot, Hypothetical* = payload.
        edit.CurrentClientId.ShouldBe(snapshotClientId);
        edit.HypotheticalClientId.ShouldBe(payloadClientId);
        edit.CurrentDueDate.ShouldBe(new DateTime(2025, 3, 20));
        edit.HypotheticalDueDate.ShouldBe(new DateTime(2025, 4, 1));
        edit.CurrentPaidAt.ShouldBe(new DateTime(2025, 3, 15));
        edit.HypotheticalPaidAt.ShouldBe(new DateTime(2025, 3, 25));
        edit.CurrentTransactionTime.ShouldBe(new TimeOnly(8, 0));
        edit.HypotheticalTransactionTime.ShouldBe(new TimeOnly(17, 45));
        edit.CurrentDescription.ShouldBe("current desc");
        edit.HypotheticalDescription.ShouldBe("new desc");

        // The snapshot is read, never written.
        snapshot.ClientId.ShouldBe(snapshotClientId);
        snapshot.DueDate.ShouldBe(new DateTime(2025, 3, 20));
        snapshot.PaidAt.ShouldBe(new DateTime(2025, 3, 15));
        snapshot.TransactionTime.ShouldBe(new TimeOnly(8, 0));
        snapshot.Description.ShouldBe("current desc");
    }

    [Fact]
    public void Constructor_ShouldCarryNulls_WhenSnapshotAndPayloadOmitOptionalFields()
    {
        var snapshot = BuildSnapshot(
            direction: Direction.In,
            value: 99m,
            date: new DateTime(2025, 1, 5),
            status: TransactionStatus.Draft,
            clientId: null,
            dueDate: new DateTime(2025, 1, 5),
            paidAt: null,
            transactionTime: null,
            description: null);

        var payload = new RequestUpdateTransactionJson
        {
            Description = null,
            DueDate = new DateTime(2025, 1, 10),
            PaidAt = null,
            ClientId = null,
            TransactionTime = null
        };

        var edit = new HypotheticalTransactionEdit(snapshot, payload);

        edit.CurrentDirection.ShouldBe(Direction.In);
        edit.HypotheticalDirection.ShouldBe(Direction.In);

        edit.CurrentClientId.ShouldBeNull();
        edit.HypotheticalClientId.ShouldBeNull();
        edit.CurrentPaidAt.ShouldBeNull();
        edit.HypotheticalPaidAt.ShouldBeNull();
        edit.CurrentTransactionTime.ShouldBeNull();
        edit.HypotheticalTransactionTime.ShouldBeNull();
        edit.CurrentDescription.ShouldBeNull();
        edit.HypotheticalDescription.ShouldBeNull();

        // The one required editable field still overlays.
        edit.CurrentDueDate.ShouldBe(new DateTime(2025, 1, 5));
        edit.HypotheticalDueDate.ShouldBe(new DateTime(2025, 1, 10));
    }

    private static Transaction BuildSnapshot(
        Direction direction,
        decimal value,
        DateTime date,
        TransactionStatus status,
        Guid? clientId,
        DateTime dueDate,
        DateTime? paidAt,
        TimeOnly? transactionTime,
        string? description)
    {
        var branch = new BranchBuilder().Build();
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Cat",
            DefaultDirection = direction,
            BranchId = branch.Id,
            Branch = branch
        };
        var transactionType = new TransactionTypeBuilder().WithCategory(category).Build();
        var account = new AccountBuilder().WithType(AccountType.Tab).WithBranch(branch).Build();

        return new TransactionBuilder()
            .WithBranch(branch)
            .WithAccount(account)
            .WithTransactionType(transactionType)
            .WithValue(value)
            .WithDate(date)
            .WithStatus(status)
            .WithClientId(clientId)
            .WithDueDate(dueDate)
            .WithPaidAt(paidAt)
            .WithTransactionTime(transactionTime)
            .WithDescription(description)
            .Build();
    }
}

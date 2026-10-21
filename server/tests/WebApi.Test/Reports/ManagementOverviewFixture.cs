using System.Net;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using WebApi.Test.TimeEntries;

namespace WebApi.Test.Reports;

/// <summary>
/// The M11 fixture recipe on one branch, built with the WebApi.Test seed helpers plus one real fiado payment (so the
/// generated Terminal leg is written by the production command). Every date is relative to branch-local today
/// <see cref="T"/>; callers freeze the host clock at <see cref="Now"/> before seeding. October is the current month,
/// September elapsed and unlocked, August locked.
/// </summary>
internal sealed class ManagementOverviewFixture
{
    public static readonly DateTime T = new(2026, 10, 20);
    public static readonly DateTime Now = T.AddHours(10);

    public required Branch Branch { get; init; }
    public required string ManagerToken { get; init; }
    public required string AdminToken { get; init; }
    public required string MemberToken { get; init; }
    public required Operator O1 { get; init; }
    public required Operator O2 { get; init; }
    public required Operator O3 { get; init; }
    public required Operator O4 { get; init; }
    public required Account T1 { get; init; }
    public required Account T2 { get; init; }
    public required Account T3 { get; init; }
    public required Account Tab1 { get; init; }
    public required Account Bank1 { get; init; }
    public required TransactionType Credit { get; init; }
    public required TransactionType Debit { get; init; }
    public required TransactionType Cheque { get; init; }
    public required Guid P1 { get; init; }
    public required Guid StandaloneCheque { get; init; }
    public required Dictionary<string, DailyClose> Closes { get; init; }
    public required HashSet<Guid> OtherBranchIds { get; init; }

    public static async Task<ManagementOverviewFixture> SeedAsync(ServerWebApplicationFactory factory, HttpClient client, string label)
    {
        var (managerUser, branch, _, managerToken) = await factory.SeedFullBranchContextAsync(label, Role.Manager);
        var adminUser = await factory.SeedUserAsync();
        var adminToken = factory.IssueBranchToken(await factory.SeedBranchUserAsync(adminUser.Id, branch.Id, Role.Admin));
        var memberUser = await factory.SeedUserAsync();
        var memberToken = factory.IssueBranchToken(await factory.SeedBranchUserAsync(memberUser.Id, branch.Id, Role.Member));
        await factory.SeedSettingAsync(branch.Id, lockDate: new DateTime(2026, 8, 31));
        await factory.SeedTimeEntryPolicyAsync(branch.Id, new DateTime(2026, 10, 10), dailyTargetHours: 6.00m);
        var variance = await factory.SeedProductAsync(branch.Id, "Diferença Caixa");

        var o1 = await factory.SeedOperatorAsync(branch.Id, "O1 Ana", userId: memberUser.Id);
        var o2 = await factory.SeedOperatorAsync(branch.Id, "O2 Bruno", userId: adminUser.Id);
        var o3 = await factory.SeedOperatorAsync(branch.Id, "O3 Carla", active: false);
        var o4 = await factory.SeedOperatorAsync(branch.Id, "O4 Davi", active: false);

        var tab1 = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "Tab1");
        var tab2 = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "Tab2");
        var t1 = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "T1", tabAccountId: tab1.Id);
        var t2 = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "T2");
        var t3 = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "T3", active: false);
        var bank1 = await factory.SeedAccountAsync(branch.Id, AccountType.BankAccount, "Bank1");
        await factory.SeedOperatorAccountAsync(o1.Id, t1.Id, isPrimary: true);
        await factory.SeedOperatorAccountAsync(o2.Id, t2.Id, isPrimary: true);

        var saidas = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var entradas = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var receita = await factory.SeedCategoryAsync(branch.Id, "Receita", Direction.In);
        var comerciais = await factory.SeedCategoryAsync(branch.Id, "Despesas Comerciais", Direction.Out);
        var credit = await factory.SeedTransactionTypeAsync(saidas.Id, "Cartão de Crédito", SettlementRule.TwoBusinessDays,
            settlementInstrument: SettlementInstrument.CreditCard);
        var debit = await factory.SeedTransactionTypeAsync(saidas.Id, "Cartão de Débito", SettlementRule.NextBusinessDay,
            settlementInstrument: SettlementInstrument.DebitCard);
        var cheque = await factory.SeedTransactionTypeAsync(saidas.Id, "Depósito Cheque", SettlementRule.OperatorEnteredCheque,
            settlementInstrument: SettlementInstrument.Cheque);
        var pix = await factory.SeedTransactionTypeAsync(saidas.Id, "PIX");
        var venda = await factory.SeedTransactionTypeAsync(entradas.Id, "Venda");
        var tarifa = await factory.SeedTransactionTypeAsync(receita.Id, "Tarifa Bolão",
            monthlyFinancialRole: MonthlyFinancialRole.BolaoServiceFee);
        var sobra = await factory.SeedTransactionTypeAsync(comerciais.Id, "Sobra de Bolão",
            monthlyFinancialRole: MonthlyFinancialRole.CommercialLeftover);
        var clienteOut = await factory.SeedTransactionTypeAsync(saidas.Id, "Cliente", requiresTabAccountAndClient: true);
        var clienteIn = await factory.SeedTransactionTypeAsync(entradas.Id, "Cliente", requiresTabAccountAndClient: true);
        var cashMethod = await factory.SeedTransactionTypeAsync(entradas.Id, "Recebimento fiado — dinheiro",
            requiresTabAccountAndClient: true, fiadoPaymentKind: FiadoPaymentKind.CountedCash);
        await factory.SeedFiadoSystemTypesAsync(branch.Id);

        async Task<Transaction> Row(TransactionType type, Category category, Account account, Operator op, DateTime date, decimal value,
            DateTime? due = null, DateTime? paidAt = null, TransactionStatus status = TransactionStatus.Active, Guid? origin = null,
            Guid? id = null, Client? clientRow = null) =>
            await factory.SeedTransactionAsync(branch.Id, account.Id, type.Id, category.Id, category.DefaultDirection, op.Id,
                managerUser.Id, date: date, value: value, dueDate: due, paidAt: paidAt, status: status, originTransactionId: origin,
                id: id, clientId: clientRow?.Id);

        // Fiado (A5/A6): A owes 100 on two Tabs; B settled; C overpaid by 30; D owes 100 and pays 40 today in cash.
        var a = await factory.SeedClientAsync(branch.Id, "Cliente A");
        var b = await factory.SeedClientAsync(branch.Id, "Cliente B");
        var c = await factory.SeedClientAsync(branch.Id, "Cliente C");
        var d = await factory.SeedClientAsync(branch.Id, "Cliente D");
        await Row(clienteOut, saidas, tab1, o1, new DateTime(2026, 10, 1), 60m, clientRow: a);
        await Row(clienteOut, saidas, tab2, o1, new DateTime(2026, 10, 2), 40m, clientRow: a);
        await Row(clienteOut, saidas, tab1, o1, new DateTime(2026, 10, 1), 50m, clientRow: b);
        await Row(clienteIn, entradas, tab1, o1, new DateTime(2026, 10, 5), 50m, clientRow: b);
        await Row(clienteOut, saidas, tab1, o1, new DateTime(2026, 10, 1), 20m, clientRow: c);
        await Row(clienteIn, entradas, tab1, o1, new DateTime(2026, 10, 6), 50m, clientRow: c);
        await Row(clienteOut, saidas, tab1, o1, new DateTime(2026, 9, 20), 100m, clientRow: d);

        // Cheques (A7/A8) on Bank1, every row 100: P1 partly paid, P2 with a cancelled row, P4 wholly open,
        // P3 wholly Draft and P5 fully paid (both excluded), plus a standalone cheque overdue by 3 days.
        var planDate = new DateTime(2026, 10, 1);
        var p1 = Guid.NewGuid();
        await Row(cheque, saidas, bank1, o1, planDate, 100m, T.AddDays(-20), paidAt: T.AddDays(-18), origin: p1, id: p1);
        await Row(cheque, saidas, bank1, o1, planDate, 100m, T.AddDays(5), origin: p1);
        await Row(cheque, saidas, bank1, o1, planDate, 100m, T.AddDays(35), origin: p1);
        var p2 = Guid.NewGuid();
        await Row(cheque, saidas, bank1, o1, planDate, 100m, T.AddDays(-10), origin: p2, id: p2);
        await Row(cheque, saidas, bank1, o1, planDate, 100m, T.AddDays(15), status: TransactionStatus.Cancelled, origin: p2);
        await Row(cheque, saidas, bank1, o1, planDate, 100m, T.AddDays(45), origin: p2);
        var p4 = Guid.NewGuid();
        await Row(cheque, saidas, bank1, o1, planDate, 100m, T.AddDays(20), origin: p4, id: p4);
        await Row(cheque, saidas, bank1, o1, planDate, 100m, T.AddDays(50), origin: p4);
        var p3 = Guid.NewGuid();
        await Row(cheque, saidas, bank1, o1, planDate, 100m, T.AddDays(3), status: TransactionStatus.Draft, origin: p3, id: p3);
        await Row(cheque, saidas, bank1, o1, planDate, 100m, T.AddDays(33), status: TransactionStatus.Draft, origin: p3);
        var p5 = Guid.NewGuid();
        await Row(cheque, saidas, bank1, o1, planDate, 100m, T.AddDays(-40), paidAt: T.AddDays(-39), origin: p5, id: p5);
        await Row(cheque, saidas, bank1, o1, planDate, 100m, T.AddDays(-10), paidAt: T.AddDays(-9), origin: p5);
        var standalone = await Row(cheque, saidas, bank1, o1, new DateTime(2026, 10, 10), 100m, T.AddDays(-3));

        // Cards (A8) on T2, recorded yesterday: due +1 and +7 inside the 14-day window, +14 outside, one paid.
        var yesterday = T.AddDays(-1);
        await Row(credit, saidas, t2, o2, yesterday, 15m, T.AddDays(1));
        await Row(debit, saidas, t2, o2, yesterday, 35m, T.AddDays(7));
        await Row(credit, saidas, t2, o2, yesterday, 25m, T.AddDays(14));
        await Row(credit, saidas, t2, o2, yesterday, 99m, T.AddDays(2), paidAt: T);

        // Closes (A2/A3): 12 in [T−6, T] once today's open Draft is counted, one at T−7 outside the window.
        var closes = new Dictionary<string, DailyClose>();
        async Task Close(string key, Account account, DateTime date, DailyCloseStatus status, decimal? retained = null,
            Operator? recorder = null, Guid? recorderUser = null, Guid? submitterUser = null, DateTime? recheck = null,
            bool itemsRecorded = true)
        {
            var close = await factory.SeedDailyCloseAsync(branch.Id, account.Id, date, status,
                submittedByOperatorId: status is DailyCloseStatus.Submitted or DailyCloseStatus.Approved ? recorder?.Id : null,
                recordedByOperatorId: recorder?.Id, recordedByUserId: recorderUser, submittedByUserId: submitterUser,
                openingRecheckRequiredAt: recheck, itemsRecorded: itemsRecorded);
            if (retained is { } value)
                await factory.SeedDailyCloseItemAsync(close.Id, variance.Id, value);
            closes[key] = close;
        }

        await Close("outside", t2, T.AddDays(-7), DailyCloseStatus.Draft);
        await Close("submitted", t1, T.AddDays(-6), DailyCloseStatus.Submitted, 10m, o1);
        await Close("approved", t2, T.AddDays(-6), DailyCloseStatus.Approved, -5m, o2);
        await Close("rejected", t1, T.AddDays(-5), DailyCloseStatus.Rejected, 3m, recorderUser: managerUser.Id, submitterUser: managerUser.Id);
        await Close("recalled", t2, T.AddDays(-5), DailyCloseStatus.Draft, 50m, o2);
        await Close("recheck", t1, T.AddDays(-4), DailyCloseStatus.Draft, 7m, o1, recheck: DateTime.UtcNow);
        await Close("d16-t2", t2, T.AddDays(-4), DailyCloseStatus.Draft, recorder: o2);
        await Close("d17-t1", t1, T.AddDays(-3), DailyCloseStatus.Draft, recorder: o1);
        await Close("d17-t2", t2, T.AddDays(-3), DailyCloseStatus.Draft, recorder: o2);
        await Close("d18-t1", t1, T.AddDays(-2), DailyCloseStatus.Draft, recorder: o1);
        await Close("d18-t2", t2, T.AddDays(-2), DailyCloseStatus.Draft, recorder: o2);
        await Close("d19-t1", t1, T.AddDays(-1), DailyCloseStatus.Draft, recorder: o1);
        await Close("today-t1", t1, T, DailyCloseStatus.Draft, itemsRecorded: false);

        // September: elapsed and unlocked with one blocker of each type; August: locked.
        await Close("sep-submitted", t1, new DateTime(2026, 9, 10), DailyCloseStatus.Submitted, 1m, o1);
        await Row(pix, saidas, t1, o1, new DateTime(2026, 9, 12), 9m, status: TransactionStatus.Draft);
        await Row(venda, entradas, t2, o2, new DateTime(2026, 9, 15), 10m);
        await Close("aug-approved", t1, new DateTime(2026, 8, 20), DailyCloseStatus.Approved, 0m, o1);

        // Today's activity (A9/A10): O1 on T1 and Bank1, a Draft row counted apart, O4 on T3 (both since deactivated).
        await Row(venda, entradas, t1, o1, T, 100m);
        await Row(pix, saidas, t1, o1, T, 30m);
        await Row(venda, entradas, t1, o1, T, 999m, status: TransactionStatus.Draft);
        await Row(venda, entradas, bank1, o1, T, 5m);
        await Row(venda, entradas, t3, o4, T, 50m);

        // Bolão (A12): the day's fee and a leftover on T1.
        await Row(tarifa, receita, t1, o1, T.AddDays(-6), 100m);
        await Row(sobra, comerciais, t1, o1, T.AddDays(-6), 20m);

        // D's cash repayment through the production command: a Tab1 In plus the generated T1 In, both recorded by O2.
        var payment = await client.PostAuthAsync("/transaction/fiado-payment", new RequestReceiveFiadoPaymentJson
        {
            Date = T,
            Value = 40m,
            TransactionTypeId = cashMethod.Id,
            AccountId = tab1.Id,
            ClientId = d.Id,
            RecordedByOperatorId = o2.Id
        }, adminToken);
        payment.StatusCode.ShouldBe(HttpStatusCode.Created, await payment.Content.ReadAsStringAsync());
        (await payment.ReadContentAsync<ResponseCreateTransactionJson>()).GeneratedTransactionId.ShouldNotBeNull();

        // Hours (A11): O1 live today; O2 with long days and a forgotten clock-out on 15 October; O3 deactivated with an entry.
        async Task Hours(Operator op, DateTime day, int fromHour, int? toHour) =>
            await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(factory, branch.Id, op.Id, day, TimeEntryStatus.Present,
                (day.AddHours(fromHour), toHour is { } to ? day.AddHours(to) : null, true));
        await Hours(o1, new DateTime(2026, 10, 5), 9, 17);
        await Hours(o1, T, 8, null);
        await Hours(o2, new DateTime(2026, 10, 6), 8, 16);
        await Hours(o2, new DateTime(2026, 10, 12), 8, 20);
        await Hours(o2, new DateTime(2026, 10, 13), 8, 20);
        await Hours(o2, new DateTime(2026, 10, 15), 8, null);
        await Hours(o3, new DateTime(2026, 10, 3), 9, 13);

        return new ManagementOverviewFixture
        {
            Branch = branch,
            ManagerToken = managerToken,
            AdminToken = adminToken,
            MemberToken = memberToken,
            O1 = o1,
            O2 = o2,
            O3 = o3,
            O4 = o4,
            T1 = t1,
            T2 = t2,
            T3 = t3,
            Tab1 = tab1,
            Bank1 = bank1,
            Credit = credit,
            Debit = debit,
            Cheque = cheque,
            P1 = p1,
            StandaloneCheque = standalone.Id,
            Closes = closes,
            OtherBranchIds = await SeedOtherBranchAsync(factory)
        };
    }

    /// <summary>B2: its own close, fiado client, cheque, card, LockDate and open shift — none may ever surface in B1.</summary>
    private static async Task<HashSet<Guid>> SeedOtherBranchAsync(ServerWebApplicationFactory factory)
    {
        var (user, branch, _, _) = await factory.SeedFullBranchContextAsync("OverviewOtherBranch", Role.Manager);
        await factory.SeedSettingAsync(branch.Id, lockDate: new DateTime(2026, 9, 30));
        var variance = await factory.SeedProductAsync(branch.Id, "Diferença Caixa");
        var op = await factory.SeedOperatorAsync(branch.Id, "B2 operator");
        var terminal = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "B2 T1");
        var tab = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "B2 Tab");
        var saidas = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var cheque = await factory.SeedTransactionTypeAsync(saidas.Id, "Depósito Cheque", SettlementRule.OperatorEnteredCheque,
            settlementInstrument: SettlementInstrument.Cheque);
        var card = await factory.SeedTransactionTypeAsync(saidas.Id, "Cartão", settlementInstrument: SettlementInstrument.CreditCard);
        var fiado = await factory.SeedTransactionTypeAsync(saidas.Id, "Cliente", requiresTabAccountAndClient: true);
        var client = await factory.SeedClientAsync(branch.Id, "B2 client");
        var close = await factory.SeedDailyCloseAsync(branch.Id, terminal.Id, T.AddDays(-6), DailyCloseStatus.Approved,
            submittedByOperatorId: op.Id, recordedByOperatorId: op.Id);
        await factory.SeedDailyCloseItemAsync(close.Id, variance.Id, 999m);
        var ids = new HashSet<Guid> { branch.Id, op.Id, terminal.Id, tab.Id, client.Id, close.Id };
        ids.Add((await factory.SeedTransactionAsync(branch.Id, tab.Id, fiado.Id, saidas.Id, Direction.Out, op.Id, user.Id,
            date: T.AddDays(-2), value: 999m, clientId: client.Id)).Id);
        ids.Add((await factory.SeedTransactionAsync(branch.Id, terminal.Id, cheque.Id, saidas.Id, Direction.Out, op.Id, user.Id,
            date: T.AddDays(-2), value: 999m, dueDate: T.AddDays(2))).Id);
        ids.Add((await factory.SeedTransactionAsync(branch.Id, terminal.Id, card.Id, saidas.Id, Direction.Out, op.Id, user.Id,
            date: T, value: 999m, dueDate: T.AddDays(1))).Id);
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(factory, branch.Id, op.Id, T, TimeEntryStatus.Present,
            (T.AddHours(7), null, true));
        return ids;
    }
}

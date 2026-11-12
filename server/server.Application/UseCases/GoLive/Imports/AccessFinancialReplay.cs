using System.Globalization;
using server.Communication.Responses;
using server.Domain.Models;

namespace server.Application.UseCases.GoLive.Imports;

/// <summary>Read-only arithmetic characterization. Missing opening/Tab evidence remains null, never zero.</summary>
internal static class AccessFinancialReplay
{
    private static readonly HashSet<string> Physical = ["2", "3", "4", "5", "6", "11"];
    private static readonly HashSet<string> FeesAndReport = ["7", "12", "14"];

    public static List<ResponseGoLiveDayJson> Compare(Dictionary<string, List<Dictionary<string, string>>> tables, IReadOnlyDictionary<string, string>? tabAssociations = null)
    {
        tabAssociations ??= new Dictionary<string, string>();
        var result = new List<ResponseGoLiveDayJson>();
        var accounts = tables["TblContas"].Where(x => x["con_tipo"] == "Terminal");
        var stockByAccount = tables["TblEstoque"].ToLookup(x => x["con_id"]);
        var ledgerByAccount = tables["TblLancamentos"].ToLookup(x => x["id_conta"]);
        var ledgerByDate = tables["TblLancamentos"].ToLookup(x => Day(x["lco_data"]));
        var tabIds = tables["TblContas"].Where(x => x["con_tipo"] == "Fiado").Select(x => x["con_id"]).ToHashSet();
        var unknownTabs = tabIds.Where(tab => !tabAssociations.ContainsKey(tab)).ToHashSet();
        var firstUnknownTabDate = tables["TblLancamentos"].Where(x => unknownTabs.Contains(x["id_conta"]))
            .Select(x => Day(x["lco_data"])).Where(x => x.HasValue).Min();
        foreach (var account in accounts)
        {
            var id = account["con_id"];
            var stock = stockByAccount[id].ToList();
            var ledger = ledgerByAccount[id].ToList();
            var dates = stock.Select(x => Day(x["est_data"])).Concat(ledger.Select(x => Day(x["lco_data"]))).Where(x => x.HasValue).Select(x => x!.Value).Distinct().Order().ToList();
            var counted = stock.Where(x => Physical.Contains(x["prod_id"])).Select(x => Day(x["est_data"])).Where(x => x.HasValue).Select(x => x!.Value).Distinct().Order().ToList();
            // No source relationship proves Tab ownership. Any Tab history keeps that side of Access unavailable.
            var stockByDate = stock.ToLookup(x => Day(x["est_data"]));
            var accountLedgerByDate = ledger.ToLookup(x => Day(x["lco_data"]));
            var associatedTabs = tabAssociations.Where(x => x.Value == id).Select(x => x.Key).ToHashSet();
            foreach (var date in dates)
            {
                var dayStock = stockByDate[date].ToList();
                var dayLedger = accountLedgerByDate[date].ToList();
                var countIndex = counted.BinarySearch(date);
                if (countIndex < 0) countIndex = ~countIndex;
                DateTime? previous = countIndex > 0 ? counted[countIndex - 1] : null;
                DateTime? priorCalendar = date == DateTime.MinValue ? null : date.AddDays(-1);
                var priorStock = stockByDate[priorCalendar].ToList();
                var priorCount = stockByDate[previous].ToList();
                bool valid = dayStock.Concat(priorStock).Concat(priorCount).All(x => Amount(x["est_valor"]) is not null)
                    && dayLedger.All(x => Amount(x["lco_valor"]) is not null && x["lco_sinal"] is "Positivo" or "Negativo");
                var closing = Sum(dayStock, Physical);
                var fees = Sum(dayStock, FeesAndReport);
                var unsigned = dayLedger.Sum(x => Amount(x["lco_valor"]) ?? 0);
                var signed = dayLedger.Sum(x => (x["lco_sinal"] == "Positivo" ? 1 : -1) * (Amount(x["lco_valor"]) ?? 0));
                bool hasCount = dayStock.Any(x => Physical.Contains(x["prod_id"]));
                bool hasPriorCalendar = priorStock.Any(x => Physical.Contains(x["prod_id"]));
                bool unknownFiado = firstUnknownTabDate.HasValue && firstUnknownTabDate <= date;
                var fiadoDelta = ledgerByDate[date].Where(x => associatedTabs.Contains(x["id_conta"]))
                    .Sum(x => (x["lco_sinal"] == "Negativo" ? 1 : -1) * (Amount(x["lco_valor"]) ?? 0));
                var terminalFiado = dayLedger.Any(x => x["id_tipo"] == "2");
                var tabActivity = ledgerByDate[date].Any(x => tabIds.Contains(x["id_conta"]));
                var access = closing + unsigned - Sum(priorStock, Physical) - fees + fiadoDelta;
                var app = previous.HasValue ? CashVarianceFormula.Calculate(closing, Sum(priorCount, Physical), Math.Max(signed + fees, 0m), Math.Max(-(signed + fees), 0m)) : (decimal?)null;
                result.Add(new ResponseGoLiveDayJson
                {
                    SourceAccountId = id,
                    Date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    RecordedAccessDifference = dayStock.Where(x => x["prod_id"] == "13").Select(x => x["est_valor"]).FirstOrDefault(),
                    SourceFiadoDelta = unknownFiado ? null : fiadoDelta.ToString(CultureInfo.InvariantCulture),
                    CountedClosing = closing.ToString(CultureInfo.InvariantCulture),
                    PriorCountedOpening = previous.HasValue ? Sum(priorCount, Physical).ToString(CultureInfo.InvariantCulture) : null,
                    UnsignedSourceMovements = unsigned.ToString(CultureInfo.InvariantCulture),
                    SignedSourceMovements = signed.ToString(CultureInfo.InvariantCulture),
                    SourceFeesAndReport = fees.ToString(CultureInfo.InvariantCulture),
                    RawAccessDifference = valid && hasCount && hasPriorCalendar ? access.ToString(CultureInfo.InvariantCulture) : null,
                    RawAppDifference = valid && hasCount && previous.HasValue ? CashVarianceFormula.Calculate(closing, Sum(priorCount, Physical), Math.Max(signed + fees, 0m), Math.Max(-(signed + fees), 0m)).ToString(CultureInfo.InvariantCulture) : null,
                    HasPriorCalendarDay = hasPriorCalendar,
                    AccessDifference = valid && hasCount && hasPriorCalendar && !unknownFiado ? MoneyOrNull(access) : null,
                    AppDifference = valid && hasCount && app.HasValue && !terminalFiado && !tabActivity ? MoneyOrNull(app.Value) : null,
                    PositiveRowDifference = 2 * dayLedger.Where(x => x["lco_sinal"] == "Positivo").Sum(x => Amount(x["lco_valor"]) ?? 0),
                    Completeness = !valid ? "InvalidSourceValue" : unknownFiado ? "MissingFiadoAssociation" : terminalFiado ? "TerminalFiadoOriginUnresolved" : tabActivity ? "FiadoMethodUnresolved" : !hasCount ? "MissingCount" : !previous.HasValue ? "MissingOpening" : "ArithmeticOnlyCustodyUnresolved"
                });
            }
        }
        return result;
    }

    private static decimal? MoneyOrNull(decimal value) => decimal.Round(value, 2) == value && decimal.Abs(value) <= 999999999999.99m ? value : null;
    private static decimal Sum(List<Dictionary<string, string>> rows, HashSet<string> products) => rows.Where(x => products.Contains(x["prod_id"])).Sum(x => Amount(x["est_valor"]) ?? 0);
    private static decimal? Amount(string text) => System.Text.RegularExpressions.Regex.IsMatch(text, "^-?[0-9]{1,12}(?:\\.[0-9]{1,18})?$") && decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) ? value : null;
    private static DateTime? Day(string text) => DateTime.TryParseExact(text, new[] { "yyyy/MM/dd H:mm:ss", "yyyy/MM/dd HH:mm:ss" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value.Date : null;
}

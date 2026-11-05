using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace server.Application.Services.Banking;

/// <summary>Validated statement evidence. Parsing has no persistence or financial effects.</summary>
public sealed record OfxStatementEvidence(
    string Fingerprint,
    string BankId,
    string AccountId,
    DateTime From,
    DateTime To,
    IReadOnlyList<OfxLineEvidence> Lines);

public sealed record OfxLineEvidence(
    string? BankIdentity,
    string Fingerprint,
    DateTime PostedOn,
    decimal Amount,
    string Type,
    string? Name,
    string? Memo,
    string Original);

/// <summary>
/// Deliberately bounded OFX 1.x SGML / OFX 2.x XML bank-statement reader. UTF-8 and Windows-1252 only; BRL,
/// one BANKMSGSRSV1 statement, no investment/credit-card statements, DTDs, entities or external resources.
/// Missing FITID remains missing: callers must retain ambiguity rather than treating equal fingerprints as payments.
/// </summary>
public static partial class OfxStatementParser
{
    public const int MaxBytes = 2 * 1024 * 1024;
    public const int MaxRows = 5000;
    public const int MaxDepth = 24;
    public const decimal MaxAmount = 999999999999.99m;
    private static readonly TimeSpan ParseBudget = TimeSpan.FromSeconds(2);
    private static readonly HashSet<string> Aggregates = [
        "OFX", "SIGNONMSGSRSV1", "SONRS", "STATUS", "FI", "BANKMSGSRSV1", "STMTTRNRS", "STMTRS",
        "BANKACCTFROM", "BANKTRANLIST", "STMTTRN", "LEDGERBAL", "AVAILBAL"];

    public static OfxStatementEvidence Parse(byte[] source, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Length is 0 or > MaxBytes)
            throw Invalid();
        ct.ThrowIfCancellationRequested();
        var started = System.Diagnostics.Stopwatch.StartNew();
        var asciiHeader = Encoding.ASCII.GetString(source, 0, Math.Min(source.Length, 1024));
        string text;
        try
        {
            if (asciiHeader.Contains("ENCODING:USASCII", StringComparison.OrdinalIgnoreCase) &&
                asciiHeader.Contains("CHARSET:1252", StringComparison.OrdinalIgnoreCase))
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                text = Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback).GetString(source);
            }
            else
                text = new UTF8Encoding(false, true).GetString(source);
        }
        catch (DecoderFallbackException) { throw Invalid(); }
        if (text.Contains("<!", StringComparison.Ordinal) || text.Contains('\0'))
            throw Invalid();

        var rootAt = text.IndexOf("<OFX>", StringComparison.OrdinalIgnoreCase);
        if (rootAt < 0) throw Invalid();
        var header = text[..rootAt].TrimStart('\uFEFF').Trim();
        var sgml = header.StartsWith("OFXHEADER:100", StringComparison.Ordinal);
        if (!sgml && !header.StartsWith("<?xml", StringComparison.Ordinal)) throw Invalid();
        if (sgml && (!header.Contains("VERSION:102", StringComparison.Ordinal) ||
                     !(header.Contains("CHARSET:1252", StringComparison.Ordinal) || header.Contains("CHARSET:UTF-8", StringComparison.Ordinal))))
            throw Invalid();
        XElement root;
        try
        {
            if (sgml)
                root = ParseSgml(text[rootAt..], started, ct);
            else
            {
                using var stringReader = new StringReader(text.TrimStart('\uFEFF'));
                using var reader = XmlReader.Create(stringReader, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxBytes,
                    MaxCharactersFromEntities = 0, IgnoreComments = false
                });
                while (reader.Read())
                {
                    CheckBudget(started, ct);
                    if (reader.NodeType == XmlNodeType.XmlDeclaration && reader.GetAttribute("encoding") is { } encoding &&
                        !encoding.Equals("UTF-8", StringComparison.OrdinalIgnoreCase)) throw Invalid();
                    if (reader.Depth > MaxDepth || reader.NodeType is XmlNodeType.Comment or XmlNodeType.DocumentType)
                        throw Invalid();
                }
                root = XElement.Parse(text[rootAt..], LoadOptions.PreserveWhitespace);
            }
            if (root.DescendantsAndSelf().Any(e => e.Name.Namespace != XNamespace.None || e.HasAttributes || !e.HasElements && e.Value.Length > 4096)) throw Invalid();
            if (root.Name.LocalName != "OFX") throw Invalid();
            var statements = root.Descendants("STMTRS").ToList();
            if (statements.Count != 1 || root.Element("BANKMSGSRSV1") is null) throw Invalid();
            var statement = statements[0];
            if (Required(statement, "CURDEF") != "BRL") throw Invalid();
            var account = One(statement, "BANKACCTFROM");
            var bankId = Required(account, "BANKID");
            var accountId = Required(account, "ACCTID");
            if (bankId.Length > 100 || accountId.Length > 100) throw Invalid();
            var transactions = One(statement, "BANKTRANLIST");
            var from = Date(Required(transactions, "DTSTART"));
            var to = Date(Required(transactions, "DTEND"));
            if (from > to) throw Invalid();
            var rows = transactions.Elements("STMTTRN").ToList();
            if (rows.Count > MaxRows) throw Invalid();
            var lines = new List<OfxLineEvidence>();
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                CheckBudget(started, ct);
                var date = Date(Required(row, "DTPOSTED"));
                if (date < from || date > to) throw Invalid();
                var amountText = Required(row, "TRNAMT");
                if (!AmountPattern().IsMatch(amountText) ||
                    !decimal.TryParse(amountText, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) ||
                    amount == 0 || Math.Abs(amount) > MaxAmount) throw Invalid();
                var type = Required(row, "TRNTYPE");
                if (type is not ("CREDIT" or "DEBIT" or "DEP" or "DIRECTDEP" or "DIRECTDEBIT" or "PAYMENT" or "FEE" or "SRVCHG" or "CHECK" or "XFER" or "OTHER" or "INT" or "POS" or "ATM"))
                    throw Invalid();
                if ((type is "CREDIT" or "DEP" or "DIRECTDEP") && amount < 0 ||
                    (type is "DEBIT" or "DIRECTDEBIT" or "PAYMENT" or "FEE" or "SRVCHG" or "CHECK" or "POS" or "ATM") && amount > 0) throw Invalid();
                var id = Optional(row, "FITID");
                if (id is not null && (id.Length > 255 || !identities.Add(id))) throw Invalid();
                var original = row.ToString(SaveOptions.DisableFormatting);
                lines.Add(new(id, CanonicalLineFingerprint(original, date, amount), date, amount, type, Optional(row, "NAME"), Optional(row, "MEMO"), original));
            }
            return new(Hash(source), bankId, accountId, from, to, lines);
        }
        catch (Exception e) when (e is XmlException or FormatException or OverflowException or RegexMatchTimeoutException)
        {
            throw Invalid();
        }
    }

    private static XElement ParseSgml(string text, System.Diagnostics.Stopwatch started, CancellationToken ct)
    {
        // Only known aggregates may nest. SGML scalar end tags are optional; aggregate end tags are mandatory.
        var stack = new Stack<XElement>();
        XElement? root = null;
        var offset = 0;
        var tags = 0;
        while (offset < text.Length)
        {
            CheckBudget(started, ct);
            var start = text.IndexOf('<', offset);
            if (start < 0)
            {
                if (!string.IsNullOrWhiteSpace(text[offset..])) throw Invalid();
                break;
            }
            if (!string.IsNullOrWhiteSpace(text[offset..start])) throw Invalid();
            var end = text.IndexOf('>', start + 1);
            if (end < 0 || end - start > 64 || ++tags > MaxRows * 40 + 100) throw Invalid();
            var tag = text[(start + 1)..end];
            offset = end + 1;
            if (tag.StartsWith('/'))
            {
                if (stack.Count == 0 || stack.Pop().Name.LocalName != tag[1..]) throw Invalid();
                continue;
            }
            if (tag.Length == 0 || tag.Any(c => c is not (>= 'A' and <= 'Z') and not (>= '0' and <= '9'))) throw Invalid();
            var element = new XElement(tag);
            if (stack.Count == 0)
            {
                if (root is not null || tag != "OFX") throw Invalid();
                root = element;
            }
            else stack.Peek().Add(element);
            if (Aggregates.Contains(tag))
            {
                stack.Push(element);
                if (stack.Count > MaxDepth) throw Invalid();
                continue;
            }
            var next = text.IndexOf('<', offset);
            if (next < 0) throw Invalid();
            var scalar = text[offset..next].Trim();
            // No entity decoding in SGML: ambiguous source data is rejected rather than interpreted.
            if (scalar.Contains('&') || scalar.Length > 4096) throw Invalid();
            element.Value = scalar;
            offset = next;
            var closing = "</" + tag + ">";
            if (text.AsSpan(offset).StartsWith(closing, StringComparison.Ordinal)) offset += closing.Length;
        }
        if (stack.Count != 0 || root is null) throw Invalid();
        return root;
    }

    private static XElement One(XElement parent, string name)
    {
        var values = parent.Elements(name).Take(2).ToList();
        return values.Count == 1 ? values[0] : throw Invalid();
    }

    private static string Required(XElement parent, string name) => Optional(parent, name) ?? throw Invalid();
    private static string? Optional(XElement parent, string name)
    {
        var values = parent.Elements(name).Take(2).ToList();
        if (values.Count > 1 || values.Any(e => e.HasElements)) throw Invalid();
        var value = values.SingleOrDefault()?.Value.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static DateTime Date(string text)
    {
        if (!DatePattern().IsMatch(text)) throw Invalid();
        var date = DateTime.ParseExact(text[..8], "yyyyMMdd", CultureInfo.InvariantCulture);
        if (date.Year is < 2000 or > 2100) throw Invalid();
        // OFX date is the bank's civil posting date; timezone metadata never shifts it to the server's zone.
        if (text.Length > 8)
        {
            _ = DateTime.ParseExact(text[..14], "yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            var bracket = text.IndexOf('[');
            if (bracket >= 0)
            {
                var zone = text[(bracket + 1)..^1].Split(':')[0];
                if (!decimal.TryParse(zone, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var hours) || hours is < -14 or > 14)
                    throw Invalid();
            }
        }
        return date;
    }

    private static void CheckBudget(System.Diagnostics.Stopwatch timer, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (timer.Elapsed > ParseBudget) throw Invalid();
    }

    internal static string CanonicalLineFingerprint(string original, DateTime date, decimal amount)
    {
        using var reader = XmlReader.Create(new StringReader(original), new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxBytes
        });
        var row = XElement.Load(reader);
        if (row.Elements().Any(e => e.HasElements)) throw Invalid();
        var fields = row.Elements().Select(e => new[]
        {
            e.Name.LocalName,
            e.Name.LocalName == "DTPOSTED" ? date.ToString("yyyyMMdd", CultureInfo.InvariantCulture) :
            e.Name.LocalName == "TRNAMT" ? amount.ToString("0.00", CultureInfo.InvariantCulture) : e.Value.Trim()
        }).OrderBy(e => e[0], StringComparer.Ordinal).ThenBy(e => e[1], StringComparer.Ordinal);
        return Hash(System.Text.Json.JsonSerializer.Serialize(fields));
    }

    private static string Hash(string value) => Hash(Encoding.UTF8.GetBytes(value));
    private static string Hash(byte[] value) => Convert.ToHexString(SHA256.HashData(value));
    private static FormatException Invalid() => new("Unsupported or invalid OFX statement.");

    [GeneratedRegex(@"^[+-]?\d{1,12}(\.\d{1,2})?$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex AmountPattern();
    [GeneratedRegex(@"^\d{8}(\d{6}(\.\d{1,3})?(\[-?\d{1,2}(\.\d+)?(:[A-Za-z]{1,8})?\])?)?$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex DatePattern();
}

using System.Text;
using Xunit;
using server.Application.Services.Banking;
using Shouldly;

namespace UseCases.Test.Services.Banking;

public class OfxStatementParserTest
{
    private const string Body = """
        <OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS><CURDEF>BRL</CURDEF>
        <BANKACCTFROM><BANKID>104</BANKID><ACCTID>synthetic-01</ACCTID></BANKACCTFROM>
        <BANKTRANLIST><DTSTART>20261001</DTSTART><DTEND>20261031</DTEND>
        <STMTTRN><TRNTYPE>CREDIT</TRNTYPE><DTPOSTED>20261005120000[-3:BRT]</DTPOSTED><TRNAMT>97.50</TRNAMT><FITID>fake-1</FITID><MEMO>Liquidação sintética</MEMO></STMTTRN>
        <STMTTRN><TRNTYPE>CREDIT</TRNTYPE><DTPOSTED>20261005120000[-3:BRT]</DTPOSTED><TRNAMT>97.50</TRNAMT><FITID>fake-2</FITID></STMTTRN>
        <STMTTRN><TRNTYPE>FEE</TRNTYPE><DTPOSTED>20261006</DTPOSTED><TRNAMT>-2.50</TRNAMT><FITID>fake-3</FITID></STMTTRN>
        </BANKTRANLIST></STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>
        """;

    internal static byte[] Xml(string body = Body) => Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"UTF-8\"?>" + body);

    [Fact]
    public void Xml_preserves_exact_decimal_sign_civil_date_source_and_distinct_equal_amount_lines()
    {
        var evidence = OfxStatementParser.Parse(Xml());
        evidence.BankId.ShouldBe("104");
        evidence.AccountId.ShouldBe("synthetic-01");
        evidence.Lines.Select(l => l.Amount).ShouldBe([97.50m, 97.50m, -2.50m]);
        evidence.Lines[0].PostedOn.ShouldBe(new DateTime(2026, 10, 5));
        evidence.Lines[0].Memo.ShouldBe("Liquidação sintética");
        evidence.Lines.Select(l => l.BankIdentity).Distinct().Count().ShouldBe(3);
        evidence.Lines[0].Original.ShouldContain("fake-1");
        OfxStatementParser.Parse(Xml()).Fingerprint.ShouldBe(evidence.Fingerprint);
    }

    [Theory]
    [InlineData("UTF-8", true)]
    [InlineData("UTF-8", false)]
    [InlineData("1252", true)]
    [InlineData("1252", false)]
    public void Sgml_supports_optional_scalar_closures_and_explicit_encodings(string charset, bool closeScalars)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var body = Body;
        if (!closeScalars)
            foreach (var scalar in new[] { "CURDEF", "BANKID", "ACCTID", "DTSTART", "DTEND", "TRNTYPE", "DTPOSTED", "TRNAMT", "FITID", "MEMO" })
                body = body.Replace("</" + scalar + ">", "");
        var encoding = charset == "1252" ? Encoding.GetEncoding(1252) : Encoding.UTF8;
        var bytes = encoding.GetBytes($"OFXHEADER:100\nDATA:OFXSGML\nVERSION:102\nENCODING:{(charset == "1252" ? "USASCII" : "UTF-8")}\nCHARSET:{charset}\n\n" + body);
        var result = OfxStatementParser.Parse(bytes);
        result.Lines.Count.ShouldBe(3);
        result.Lines[0].Memo.ShouldBe("Liquidação sintética");
    }

    [Theory]
    [InlineData("20261001", "19990101")]
    [InlineData("20261031", "21010101")]
    [InlineData("20261005120000[-3:BRT]", "20261005120000[15:BRT]")]
    [InlineData("BRL", "USD")]
    [InlineData("97.50", "97,50")]
    [InlineData("97.50", "97.501")]
    [InlineData("97.50", "0")]
    [InlineData("97.50", "-97.50")]
    [InlineData("fake-2", "fake-1")]
    [InlineData("20261006", "20261101")]
    [InlineData("20261005120000[-3:BRT]", "20261005250000")]
    [InlineData("20261005120000[-3:BRT]", "20261005120000[-15:BRT]")]
    [InlineData("</OFX>", "")]
    [InlineData("<CURDEF>BRL</CURDEF>", "<CURDEF>BRL</CURDEF><CURDEF>BRL</CURDEF>")]
    public void Rejects_invalid_or_ambiguous_evidence(string before, string after) =>
        Should.Throw<FormatException>(() => OfxStatementParser.Parse(Xml(Body.Replace(before, after))));

    [Theory]
    [InlineData("</OFX>", "")]
    [InlineData("</BANKTRANLIST>", "</STMTRS>")]
    [InlineData("</OFX>", "</OFX>unexpected")]
    [InlineData("</OFX>", "</OFX><OFX></OFX>")]
    [InlineData("<TRNAMT>97.50", "<TRNAMT attr=x>97.50")]
    [InlineData("<MEMO>Liquidação sintética", "<MEMO>&external;")]
    [InlineData("<CURDEF>BRL", "<CURDEF>BRL<CURDEF>BRL")]
    public void SgmlRejectsTruncationWrongClosuresTrailingDataAttributesAndEntities(string before, string after)
    {
        var body = Body;
        foreach (var scalar in new[] { "CURDEF", "BANKID", "ACCTID", "DTSTART", "DTEND", "TRNTYPE", "DTPOSTED", "TRNAMT", "FITID", "MEMO" })
            body = body.Replace("</" + scalar + ">", "");
        var source = "OFXHEADER:100\nVERSION:102\nENCODING:UTF-8\nCHARSET:UTF-8\n\n" + body.Replace(before, after);
        Should.Throw<FormatException>(() => OfxStatementParser.Parse(Encoding.UTF8.GetBytes(source)));
    }

    [Fact]
    public void XmlRejectsUnsupportedDeclaredEncodingOversizedScalarsAndDuplicateAccountContainers()
    {
        Should.Throw<FormatException>(() => OfxStatementParser.Parse(Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"UTF-16\"?>" + Body)));
        Should.Throw<FormatException>(() => OfxStatementParser.Parse(Xml(Body.Replace("Liquidação sintética", new string('a', 4097)))));
        var account = "<BANKACCTFROM><BANKID>104</BANKID><ACCTID>synthetic-01</ACCTID></BANKACCTFROM>";
        Should.Throw<FormatException>(() => OfxStatementParser.Parse(Xml(Body.Replace(account, account + account))));
    }

    [Fact]
    public void SgmlBoundsScalarsTagsAndKnownAggregateNesting_AndAllowsTrailingWhitespace()
    {
        const string header = "OFXHEADER:100\nVERSION:102\nENCODING:UTF-8\nCHARSET:UTF-8\n\n";
        OfxStatementParser.Parse(Encoding.UTF8.GetBytes(header + Body + " \r\n")).Lines.Count.ShouldBe(3);
        foreach (var body in new[]
        {
            Body.Replace("Liquidação sintética", new string('a', 4097)),
            Body.Replace("<OFX>", "<OFX>" + string.Concat(Enumerable.Repeat("<FI>", 25))),
            Body.Replace("<OFX>", "<OFX><" + new string('A', 65) + ">"),
            Body.Replace("<OFX>", "<OFX><>"),
            Body.Replace("<OFX>", "<OFX>unexpected")
        })
            Should.Throw<FormatException>(() => OfxStatementParser.Parse(Encoding.UTF8.GetBytes(header + body)));
    }

    [Fact]
    public void EmptyFitidRemainsMissing_AndStatementHashDiscriminatesExactSourceBytes()
    {
        var a = OfxStatementParser.Parse(Xml(Body.Replace("<FITID>fake-1</FITID>", "<FITID> </FITID>")));
        a.Lines[0].BankIdentity.ShouldBeNull();
        var b = OfxStatementParser.Parse(Xml(Body.Replace("Liquidação sintética", "Outra evidência sintética")));
        a.Fingerprint.ShouldNotBe(b.Fingerprint);
        var original = OfxStatementParser.Parse(Xml());
        original.Lines[0].Fingerprint.ShouldNotBe(b.Lines[0].Fingerprint);
        original.Lines[1].Fingerprint.ShouldBe(b.Lines[1].Fingerprint);
    }

    [Fact]
    public void Missing_identity_is_retained_and_never_assigned_a_fictitious_bank_identity()
    {
        var result = OfxStatementParser.Parse(Xml(Body.Replace("<FITID>fake-1</FITID>", "").Replace("<FITID>fake-2</FITID>", "")));
        result.Lines[0].BankIdentity.ShouldBeNull();
        result.Lines[1].BankIdentity.ShouldBeNull();
        result.Lines[0].Fingerprint.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Rejects_entities_oversize_invalid_encoding_and_cancellation()
    {
        Should.Throw<FormatException>(() => OfxStatementParser.Parse(Xml("<!DOCTYPE OFX [<!ENTITY x SYSTEM 'https://invalid.test/x'>]>" + Body)));
        Should.Throw<FormatException>(() => OfxStatementParser.Parse(new byte[OfxStatementParser.MaxBytes + 1]));
        Should.Throw<FormatException>(() => OfxStatementParser.Parse([0xff, 0xff]));
        Should.Throw<OperationCanceledException>(() => OfxStatementParser.Parse(Xml(), new CancellationToken(true)));
    }

    [Fact]
    public void Rejects_row_and_depth_limits()
    {
        var row = "<STMTTRN><TRNTYPE>CREDIT</TRNTYPE><DTPOSTED>20261005</DTPOSTED><TRNAMT>1</TRNAMT></STMTTRN>";
        var start = Body.IndexOf("<STMTTRN>", StringComparison.Ordinal);
        var end = Body.IndexOf("</BANKTRANLIST>", StringComparison.Ordinal);
        var oversized = Body[..start] + string.Concat(Enumerable.Repeat(row, OfxStatementParser.MaxRows + 1)) + Body[end..];
        Should.Throw<FormatException>(() => OfxStatementParser.Parse(Xml(oversized)));
        Should.Throw<FormatException>(() => OfxStatementParser.Parse(Xml(Body.Replace("<CURDEF>", string.Concat(Enumerable.Repeat("<NEST>", 30)) + "<CURDEF>"))));
    }
    [Fact]
    public void SemanticLineFingerprintIgnoresLayoutTimestampPrecisionAndSignedAmountFormatting()
    {
        var original = OfxStatementParser.Parse(Xml());
        var formatted = System.Xml.Linq.XElement.Parse(Body).ToString()
            .Replace("20261005120000[-3:BRT]", "20261005143000[-3:BRT]")
            .Replace("<TRNAMT>97.50", "<TRNAMT>+97.5");
        var reexport = OfxStatementParser.Parse(Xml(formatted));
        reexport.Fingerprint.ShouldNotBe(original.Fingerprint);
        reexport.Lines.Select(l => l.Fingerprint).ShouldBe(original.Lines.Select(l => l.Fingerprint));
        reexport.Lines[0].Original.ShouldNotBe(original.Lines[0].Original);
    }

    [Theory]
    [InlineData("INT", "1.00")]
    [InlineData("INT", "-1.00")]
    [InlineData("ATM", "-1.00")]
    [InlineData("POS", "-1.00")]
    public void SupportsExplicitInterestAndWithdrawalTypesWithoutInferringDirection(string type, string amount)
    {
        var body = Body.Replace("<TRNTYPE>CREDIT", "<TRNTYPE>" + type).Replace("<TRNAMT>97.50", "<TRNAMT>" + amount);
        OfxStatementParser.Parse(Xml(body)).Lines[0].Amount.ShouldBe(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture));
    }

}

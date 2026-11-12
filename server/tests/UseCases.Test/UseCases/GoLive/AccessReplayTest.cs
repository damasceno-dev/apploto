using System.Text;
using CommonTestUtilities.Requests;
using server.Application.UseCases.GoLive.Imports;
using server.Communication.Requests;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.GoLive;

public class AccessReplayTest
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Replay_matches_literal_signed_fee_and_associated_debt_totals(bool withTab, bool netOutgoing)
    {
        var request = RequestGoLiveImportJsonBuilder.BuildWindows();
        RequestGoLiveImportJsonBuilder.ReplaceTable(request, "TblContas", "1,Box,,,Terminal\n2,Tab,,,Fiado\n");
        RequestGoLiveImportJsonBuilder.ReplaceTable(request, "TblEstoque", "1,2026/11/01 0:00:00,6,100,1,2026/11/01 0:00:00\n2,2026/11/02 0:00:00,6,120,1,2026/11/02 0:00:00\n3,2026/11/02 0:00:00,7,5,1,2026/11/02 0:00:00\n4,2026/11/02 0:00:00,12,-2,1,2026/11/02 0:00:00\n");
        var ledger = "1,2026/11/02 0:00:00,10,1,1,In,,2026/11/02 0:00:00,,1,Positivo,1,,,,2026/11/02 0:00:00\n2,2026/11/02 0:00:00,4,1,1,Out,,2026/11/02 0:00:00,,1,Negativo,1,,,,2026/11/02 0:00:00\n";
        if (withTab) ledger += "3,2026/11/02 0:00:00,12,2,2,Debt,,2026/11/02 0:00:00,,2,Negativo,1,,,,2026/11/02 0:00:00\n4,2026/11/02 0:00:00,5,2,1,Payment,,2026/11/02 0:00:00,,2,Positivo,1,,,,2026/11/02 0:00:00\n";
        if (netOutgoing) ledger = ledger.Replace(",10,1,1,In", ",4,1,1,In").Replace(",4,1,1,Out", ",10,1,1,Out");
        RequestGoLiveImportJsonBuilder.ReplaceTable(request, "TblLancamentos", ledger);
        var source = AccessSourceReader.Read(request);
        var day = AccessFinancialReplay.Compare(source.Tables, new Dictionary<string, string> { ["2"] = "1" })[1];
        day.UnsignedSourceMovements.ShouldBe("14");
        day.SignedSourceMovements.ShouldBe(netOutgoing ? "-6" : "6");
        day.SourceFeesAndReport.ShouldBe("3");
        day.SourceFiadoDelta.ShouldBe(withTab ? "7" : "0");
        // Access: 120 + 14 - 100 - 3 + (12 - 5). App: 120 - 100 - (10 - 4 + 3).
        day.AccessDifference.ShouldBe(withTab ? 38m : 31m);
        day.RawAccessDifference.ShouldBe(withTab ? "38" : "31");
        day.RawAppDifference.ShouldBe(netOutgoing ? "23" : "11");
        if (withTab) day.AppDifference.ShouldBeNull(); else day.AppDifference.ShouldBe(netOutgoing ? 23m : 11m);
        day.PositiveRowDifference.ShouldBe(netOutgoing ? 8m : 20m);
        day.Completeness.ShouldBe(withTab ? "FiadoMethodUnresolved" : "ArithmeticOnlyCustodyUnresolved");
    }

    [Theory]
    [InlineData("amount", "InvalidSourceValue")]
    [InlineData("sign", "InvalidSourceValue")]
    [InlineData("unknown-tab", "MissingFiadoAssociation")]
    [InlineData("future-tab", "ArithmeticOnlyCustodyUnresolved")]
    [InlineData("terminal-fiado", "TerminalFiadoOriginUnresolved")]
    [InlineData("count", "MissingCount")]
    [InlineData("opening", "MissingOpening")]
    public void Unavailable_source_evidence_keeps_its_reason_and_only_supported_comparisons(string defect, string reason)
    {
        var request = RequestGoLiveImportJsonBuilder.BuildWindows();
        RequestGoLiveImportJsonBuilder.ReplaceTable(request, "TblContas", "1,Box,,,Terminal\n2,Tab,,,Fiado\n");
        RequestGoLiveImportJsonBuilder.ReplaceTable(request, "TblEstoque", "1,2026/11/01 0:00:00,6,100,1,2026/11/01 0:00:00\n2,2026/11/02 0:00:00,6,120,1,2026/11/02 0:00:00\n");
        RequestGoLiveImportJsonBuilder.ReplaceTable(request, "TblLancamentos", "1,2026/11/02 0:00:00,10,1,1,Movement,,2026/11/02 0:00:00,,1,Positivo,1,,,,2026/11/02 0:00:00\n2,2026/11/03 0:00:00,5,1,1,Tab,,2026/11/03 0:00:00,,2,Negativo,1,,,,2026/11/03 0:00:00\n");
        var source = AccessSourceReader.Read(request);
        switch (defect)
        {
            case "amount": source.Row("TblLancamentos", "1")["lco_valor"] = "invalid"; break;
            case "sign": source.Row("TblLancamentos", "1")["lco_sinal"] = "unknown"; break;
            case "unknown-tab": source.Row("TblLancamentos", "2")["lco_data"] = "2026/11/01 0:00:00"; break;
            case "terminal-fiado": source.Row("TblLancamentos", "1")["id_tipo"] = "2"; break;
            case "count": source.Tables["TblEstoque"].RemoveAt(1); break;
            case "opening": source.Tables["TblEstoque"].RemoveAt(0); break;
        }
        var day = AccessFinancialReplay.Compare(source.Tables).Single(x => x.Date == "2026-11-02");
        day.Completeness.ShouldBe(reason);
        if (defect == "future-tab") { day.AppDifference.ShouldBe(10m); day.SourceFiadoDelta.ShouldBe("0"); }
        else if (defect == "unknown-tab") day.AppDifference.ShouldBe(10m);
        else day.AppDifference.ShouldBeNull();
        if (defect == "unknown-tab") { day.SourceFiadoDelta.ShouldBeNull(); day.AccessDifference.ShouldBeNull(); }
        if (defect is "amount" or "sign" or "count") { day.RawAccessDifference.ShouldBeNull(); day.RawAppDifference.ShouldBeNull(); }
        if (defect == "opening") { day.PriorCountedOpening.ShouldBeNull(); day.RawAppDifference.ShouldBeNull(); }
    }

    [Theory]
    [InlineData("windows", "cp1252")]
    [InlineData("mestre-1.5.0", "utf-8-sig")]
    public void Same_bytes_cannot_silently_change_interpretation(string profile, string encoding)
    {
        var recorded = RequestGoLiveImportJsonBuilder.BuildWindows();
        var requested = RequestGoLiveImportJsonBuilder.BuildWindows();
        requested.Profile = profile;
        requested.Encoding = encoding;
        Should.Throw<ConflictException>(() => GoLiveImportUseCase.EnsureSameInterpretation(recorded, requested));
        GoLiveImportUseCase.EnsureSameInterpretation(recorded, recorded);
    }

    [Fact]
    public void Source_index_resolves_every_row_at_the_supported_limit()
    {
        var request = RequestGoLiveImportJsonBuilder.BuildWindows();
        foreach (var file in request.Files.ToList())
            RequestGoLiveImportJsonBuilder.ReplaceTable(request, file.Name[..^4], "");
        var rows = new StringBuilder();
        for (var id = 1; id <= AccessSourceReader.MaxRows; id++) rows.Append(id).Append(",Synthetic,,,,,,,,\n");
        RequestGoLiveImportJsonBuilder.ReplaceTable(request, "TblClientes", rows.ToString());
        var source = AccessSourceReader.Read(request);
        for (var id = 1; id <= AccessSourceReader.MaxRows; id++)
            source.Row("TblClientes", id.ToString())["cli_id"].ShouldBe(id.ToString());
        source.Find("TblClientes", "missing").ShouldBeNull();
    }

    [Fact]
    public void Positive_rows_explain_two_values_and_date_gap_keeps_last_counted_opening()
    {
        var request=RequestGoLiveImportJsonBuilder.BuildWindows();
        RequestGoLiveImportJsonBuilder.ReplaceTable(request,"TblContas","1,Synthetic box,,,Terminal\n");
        RequestGoLiveImportJsonBuilder.ReplaceTable(request,"TblEstoque","1,2026/11/01 0:00:00,6,100,1,2026/11/01 0:00:00\n2,2026/11/02 0:00:00,6,110,1,2026/11/02 0:00:00\n3,2026/11/04 0:00:00,6,120,1,2026/11/04 0:00:00\n");
        RequestGoLiveImportJsonBuilder.ReplaceTable(request,"TblLancamentos","1,2026/11/02 0:00:00,10,1,1,Synthetic,,2026/11/02 0:00:00,,1,Positivo,1,,,,2026/11/02 0:00:00\n");
        var source=AccessSourceReader.Read(request);
        var days=AccessFinancialReplay.Compare(source.Tables);
        days.Count.ShouldBe(3);
        days[0].AppDifference.ShouldBeNull();
        days[1].AppDifference.ShouldBe(0m);
        days[1].AccessDifference.ShouldBe(20m);
        days[1].PositiveRowDifference.ShouldBe(20m);
        days[2].HasPriorCalendarDay.ShouldBeFalse();
        days[2].AccessDifference.ShouldBeNull();
        days[2].AppDifference.ShouldBe(10m);
        days[2].PriorCountedOpening.ShouldBe("110");
        AccessSourceReader.Read(request).Digest.ShouldBe(source.Digest);
        request.Files.Reverse();
        AccessSourceReader.Read(request).Digest.ShouldBe(source.Digest);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("path")]
    [InlineData("quote")]
    [InlineData("utf8")]
    [InlineData("version")]
    public void Rejects_unsupported_or_corrupted_exports(string defect)
    {
        var request=RequestGoLiveImportJsonBuilder.BuildMestre();
        switch(defect)
        {
            case "duplicate": request.Files[0]=request.Files[1]; break;
            case "path": request.Files[0].Name="../"+request.Files[0].Name;break;
            case "quote": RequestGoLiveImportJsonBuilder.ReplaceTable(request,"TblContas","1,\"truncated");break;
            case "utf8": request.Files[0].Base64=Convert.ToBase64String([0xff,0xfe]);break;
            case "version": RequestGoLiveImportJsonBuilder.ReplaceTable(request,"TblVersaoBE","1,9.0,2026/11/01 0:00:00\n");break;
        }
        Should.Throw<OnValidationException>(()=>AccessSourceReader.Read(request));
    }

    [Fact]
    public void Never_rounds_raw_precision_into_an_accepted_cent()
    {
        Should.Throw<OnValidationException>(()=>GoLiveHistoryStage.Value(new RequestGoLiveResolutionJson(),"125.500000000000000001"));
        GoLiveHistoryStage.Value(new RequestGoLiveResolutionJson {TargetValue=125.50m},"125.500000000000000001").ShouldBe(125.50m);
    }
}

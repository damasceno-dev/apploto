using System.Globalization;
using System.Net;
using System.Text;
using server.Communication.Responses;
namespace server.Application.Services.Reports;

/// <summary>Server-rendered, print-ready HTML. Decimal formatting works with invariant globalization enabled.</summary>
public static class EmployeeStatementPrintRenderer
{
    private static string E(object? value) => WebUtility.HtmlEncode(value?.ToString() ?? "Não informado");
    private static string N(decimal? value, int digits = 2) => value is null ? "Não comprovado" :
        value.Value.ToString("N" + digits, CultureInfo.InvariantCulture).Replace(",", "|").Replace(".", ",").Replace("|", ".");
    private static string D(DateTime value) => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    public static string Render(ResponseEmployeeStatementJson s)
    {
        if (s.Differences.TotalCount > s.Differences.Entries.Count)
            throw new server.Exceptions.Exceptions.ConflictException(server.Exceptions.ResourcesErrorMessages.PEOPLE_REPORT_TOO_LARGE);
        var html = new StringBuilder("<!doctype html><html lang=\"pt-BR\"><meta charset=\"utf-8\"><title>Demonstrativo da pessoa</title><style>" +
            "@page{size:A4;margin:16mm}body{font:12px system-ui;color:#171717;line-height:1.45}h1{font-size:23px}h2{font-size:16px;margin-top:24px;break-after:avoid}table{width:100%;border-collapse:collapse;margin:12px 0;table-layout:fixed}th,td{border:1px solid #aaa;padding:6px;vertical-align:top;overflow-wrap:anywhere}th{text-align:left;background:#eee}thead{display:table-header-group}tr{break-inside:avoid}.amount{text-align:right;font-variant-numeric:tabular-nums}.note{border-left:3px solid #555;padding:8px}small{color:#444}.id{font-size:9px} @media print{button{display:none}}" +
            "</style><body><h1>Demonstrativo mensal da pessoa</h1>");
        html.Append($"<h2>{E(s.OperatorName)}</h2><p>{D(s.DateFrom)} a {D(s.DateTo)} · Corte: {D(s.AsOfDate)} · Contrato v{s.ContractRevision} · {(s.IsLockedPeriod ? "Período bloqueado" : "Período aberto")}</p>");
        html.Append($"<p class=id>Pessoa: {s.OperatorId} · Cadastro {(s.OperatorActive ? "ativo" : "inativo, histórico preservado")}</p>");
        html.Append("<p class=note>Documento gerencial. Não comprova pagamento. Comissão proposta, diferenças de caixa e horas são informações distintas dos itens registrados. Nenhum desconto é automático.</p>");
        html.Append("<h2>Itens registrados</h2><table><thead><tr><th>Origem / publicação</th><th>Tipo e evidência</th><th>Contribuição (R$)</th></tr></thead><tbody>");
        foreach (var item in s.PayItems)
            html.Append($"<tr><td>{D(item.SourceDate)} / {D(item.PostedOn)}<p class=id>{item.Id}<br>Anterior: {E(item.PreviousId)}</p></td><td>{E(item.CategoryName)}<br>{(item.Kind == server.Domain.Entities.Enums.PayItemKind.Addition ? "Acréscimo" : "Desconto")}{(item.Cancelled ? " · Cancelamento" : "")}<br>{E(item.Reason)}<br>{E(item.Evidence)}<br>Política: {E(item.DeductionPolicy)}<br>Origem da comissão: {E(item.CommissionCalculationId)}</td><td class=amount>{N(item.SignedContribution)}</td></tr>");
        html.Append($"</tbody></table><p>Acréscimos: R$ {N(s.RecordedAdditions)} · Descontos: R$ {N(s.RecordedDeductions)} · Total registrado: R$ {N(s.RecordedPaySignedTotal)}. Situação: registrado, sem confirmação de pagamento.</p>");
        html.Append($"<h2>Comissão calculada — proposta informativa</h2><p>Base conhecida: R$ {N(s.Commission.KnownBasis)} · Proposta: R$ {N(s.Commission.ProposedAmount)} · Completude: {Completeness(s.Commission.Completeness)}</p><p class=id>Cálculo: {E(s.Commission.CalculationId)}</p>");
        html.Append($"<p>Comissão registrada para o mês de origem: R$ {N(s.Commission.RecordedSourceMonthAmount)} · Diferença para a proposta: R$ {N(s.Commission.DifferenceFromRecorded)} · Revisão explícita necessária: {(s.Commission.RequiresReconciliation ? "Sim" : "Não")}.</p>");
        foreach (var rule in s.Commission.Rules) html.Append($"<p>Regra {rule.Id}, versão {rule.Version}: {D(rule.EffectiveFrom)} a {D(rule.EffectiveTo)}, {N(rule.RatePercent, 4)}%. Publicada em {D(rule.PostedOn)}. {E(rule.Evidence)}</p>");
        foreach (var missing in s.Commission.MissingInputs) html.Append($"<p>{E(missing)}</p>");
        html.Append("<table><thead><tr><th>Origem / regra</th><th>Data / publicação</th><th>Base (R$)</th></tr></thead><tbody>");
        foreach (var basis in s.Commission.Basis) html.Append($"<tr><td class=id>{basis.SourceId}<br>{E(basis.RuleId)}<br>{E(basis.Evidence)}<br>{(basis.UnroundedCommission == null ? "Fora do cálculo / regra indisponível" : "Incluída na base da regra")}</td><td>{D(basis.SourceDate)} / {D(basis.PostedOn)}</td><td class=amount>{N(basis.SignedAmount)}</td></tr>");
        html.Append("</tbody></table><h2>Sessões sob custódia</h2><table><thead><tr><th>Data / sessão</th><th>Custódia / autoria / envio</th></tr></thead><tbody>");
        foreach (var session in s.Sessions) html.Append($"<tr><td>{D(session.SourceDate)}<p class=id>{session.SessionId}<br>Tentativa oficial: {E(session.OfficialAttemptId)}</p></td><td class=id>Responsável: {E(session.CustodianOperatorId)}<br>Registrado por: {E(session.RecorderOperatorId)}<br>Enviado por: {E(session.SubmitterOperatorId)}</td></tr>");
        html.Append("</tbody></table><h2>Diferenças — informação, sem desconto automático</h2><table><thead><tr><th>Origem / publicação</th><th>Falta (R$)</th><th>Sobra (R$)</th></tr></thead><tbody>");
        foreach (var d in s.Differences.Entries) html.Append($"<tr><td class=id>{d.SourceId}<br>{D(d.SourceDate)} / {D(d.PostedOn)}</td><td class=amount>{N(d.ShortageContribution)}</td><td class=amount>{N(d.OverageContribution)}</td></tr>");
        html.Append($"</tbody></table><p>{s.UnassignedInformationalSourceCount} origens sem responsabilidade pessoal atribuída na filial. Compensações de ajustes podem produzir totais negativos.</p>");
        var h = s.HourBank;
        html.Append($"<h2>Frequência e banco de horas</h2><p>Horas de frequência no período: {N(s.Attendance.TotalHours, 4)} h. Contribuição da frequência: {N(h.AttendanceContributionHours, 4)} h.</p><p>Saldo anterior: {N(h.CarryOverHours, 4)} h · Abertura no período: {N(h.PeriodOpeningHours, 4)} h · Pagamentos: {N(h.PaymentHours, 4)} h · Ajustes: {N(h.AdjustmentHours, 4)} h · Saldo final: {N(h.ClosingBalanceHours, 4)} h.</p><p>Completude: {Completeness(h.Completeness)} · Em andamento: {(h.ContainsInProgress ? "Sim" : "Não")}. {E(h.MissingInput)}</p><p class=id>Origem inicial: {E(h.OpeningSourceId)} · Início comprovado: {(h.OpeningSourceDate is { } openingDate ? D(openingDate) : "Não comprovado")}. Datas anteriores não são presumidas como histórico verificado.</p>");
        html.Append("<table><thead><tr><th>Data</th><th>Horas de frequência</th><th>Contribuição de horas</th></tr></thead><tbody>");
        foreach (var day in h.AttendanceDays) html.Append($"<tr><td>{D(day.Date)}</td><td class=amount>{N(day.TotalHours, 4)}</td><td class=amount>{N(day.BalanceHours, 4)}</td></tr>");
        html.Append("</tbody></table><table><thead><tr><th>Registro / origem / publicação</th><th>Justificativa</th><th>Horas</th></tr></thead><tbody>");
        foreach (var entry in h.Entries) html.Append($"<tr><td class=id>{entry.Id}<br>{D(entry.SourceDate)} / {D(entry.PostedOn)}<br>{HourKind(entry.Kind)}</td><td>{E(entry.Reason)}<br>{E(entry.Evidence)}</td><td class=amount>{N(entry.SignedContribution, 4)}</td></tr>");
        html.Append($"</tbody></table><h2>Sorteios especiais — distribuição comprovada</h2><p>Tarifa Especial oficial: R$ {N(s.SpecialDraws.SourceFeeTotal)} · Distribuição publicada no período: R$ {N(s.SpecialDraws.AllocatedFeeTotal)} · Saldo sem distribuição no período: R$ {N(s.SpecialDraws.UnallocatedFeeTotal)}.</p>");
        if (s.SpecialDraws.UnallocatedFeeTotal < 0)
            html.Append("<p>Saldo sem distribuição negativo: há alocações publicadas neste período para tarifas publicadas em outro mês. O valor representa movimentos do período.</p>");
        foreach (var draw in s.SpecialDraws.Draws) html.Append($"<p>{E(draw.Name)}: R$ {N(draw.AllocatedFee)} <small>{draw.DrawId}</small></p>");
        html.Append($"<p>Mapeamento de origens: {(s.SpecialDraws.SourceMappingComplete ? "Coerente" : "Incompleto; verificar revisões das origens")}.</p><h2>Proveniência e limites</h2>");
        foreach (var limit in s.Limitations) html.Append($"<p>{E(limit)}</p>");
        return html.Append("</body></html>").ToString();
    }
    private static string Completeness(server.Domain.Entities.Enums.PeopleSourceCompleteness value) => value switch
    {
        server.Domain.Entities.Enums.PeopleSourceCompleteness.Complete => "Completo para as origens declaradas",
        server.Domain.Entities.Enums.PeopleSourceCompleteness.Incomplete => "Incompleto",
        _ => "Não configurado"
    };
    private static string HourKind(server.Domain.Entities.Enums.HourBankEntryKind value) => value switch
    {
        server.Domain.Entities.Enums.HourBankEntryKind.Opening => "Saldo inicial comprovado",
        server.Domain.Entities.Enums.HourBankEntryKind.Payment => "Pagamento de horas",
        _ => "Ajuste de horas"
    };
}

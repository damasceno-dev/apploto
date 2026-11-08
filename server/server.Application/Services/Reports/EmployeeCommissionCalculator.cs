using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using server.Application.UseCases.People;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Models;
namespace server.Application.Services.Reports;

public static class EmployeeCommissionCalculator
{
    public static ResponseEmployeeCommissionJson Calculate(Guid operatorId, DateTime from, DateTime to, DateTime cutoff,
        IReadOnlyList<EmployeeCommissionRule> rules, IReadOnlyList<EmployeeCommissionBasisSource> postedSources,
        IReadOnlyList<EmployeeCommissionSource> explicitSources, int page, int pageSize)
    {
        var applicable = PeopleHistory.Heads(rules.Where(r => r.OperatorId == operatorId && r.PostedOn <= cutoff), r => r.PreviousId)
            .Where(r => r.Enabled).ToList();
        var result = new ResponseEmployeeCommissionJson { DateFrom = from, DateTo = to, AsOfDate = cutoff };
        var sources = postedSources.Select(s => new ResponseCommissionBasisJson
        {
            PostingHistoryComplete = s.PostingHistoryComplete,
            SourceId = s.SourceId,
            TransactionId = s.TransactionId,
            SessionId = s.SessionId,
            SourceDate = s.SourceDate,
            PostedOn = s.PostedOn,
            Component = s.Component,
            SignedAmount = s.SignedAmount,
            ClassificationProvenance = s.Provenance,
            Evidence = s.Provenance switch
            {
                ClassificationProvenance.Posting => "Classificação congelada na publicação; custódia oficial da sessão.",
                ClassificationProvenance.OpenPeriodReclassification => "Classificação revisada em período aberto; custódia oficial da sessão.",
                ClassificationProvenance.MigratedFromTypeAtUpgrade => "Classificação copiada na atualização; origem histórica não comprovada.",
                _ => "Classificação sem proveniência comprovada."
            }
        }).Concat(explicitSources.Where(s => s.OperatorId == operatorId && s.PostedOn >= from && s.PostedOn <= to)
            .Select(s => new ResponseCommissionBasisJson
            {
                PostingHistoryComplete = true,
                SourceId = s.Id,
                SourceDate = s.SourceDate,
                PostedOn = s.PostedOn,
                Component = CommissionBasisComponent.ExplicitSource,
                SignedAmount = s.SignedContribution,
                Evidence = (s.SourceRole == "EnteredPrize" ? "Prêmio informado" : "Sobra de Bolão") + ": " + s.Evidence
            })).OrderBy(s => s.PostedOn).ThenBy(s => s.SourceId).ToList();
        // Every date in the effective window must have exactly one evidenced employee rule; no enrollment/default.
        for (var day = from; day <= to; day = day.AddDays(1))
            if (applicable.Count(r => r.EffectiveFrom <= day && r.EffectiveTo >= day) != 1)
            { result.MissingInputs.Add("Gestor: configurar regra explícita para todas as datas do período."); break; }
        foreach (var source in sources)
        {
            if (!source.PostingHistoryComplete)
                result.MissingInputs.Add("Gestor: comprovar a publicação da tarifa " + source.SourceId + "; a data econômica não comprova o mês de publicação.");
            if (source.Component != CommissionBasisComponent.ExplicitSource &&
                source.ClassificationProvenance is null or ClassificationProvenance.MigratedFromTypeAtUpgrade)
                result.MissingInputs.Add("Gestor: revisar a classificação histórica da origem " + source.SourceId + "; a classificação original não está comprovada.");
            var candidates = applicable.Where(r => r.EffectiveFrom <= source.SourceDate && r.EffectiveTo >= source.SourceDate).ToList();
            var rule = candidates.Count == 1 ? candidates[0] : null;
            if (rule == null) { result.MissingInputs.Add("Gestor: informar a regra comprovada para a data da origem " + source.SourceId + "."); continue; }
            if (!(source.Component switch
            {
                CommissionBasisComponent.BolaoServiceFee => rule.IncludeBolaoFee,
                CommissionBasisComponent.SpecialDrawServiceFee => rule.IncludeSpecialFee,
                CommissionBasisComponent.ExplicitSource => rule.IncludeExplicitSources,
                _ => false
            })) continue;
            source.RuleId = rule.Id; source.RatePercent = rule.RatePercent;
            source.UnroundedCommission = source.SignedAmount * rule.RatePercent / 100m;
        }
        // Explicit sources are a declared input ledger, not an inferred allocation of legacy prizes/leftovers.
        if (applicable.Any(r => r.EffectiveFrom <= to && r.EffectiveTo >= from && r.IncludeExplicitSources) && !sources.Any(s => s.Component == CommissionBasisComponent.ExplicitSource && s.RuleId != null))
            result.MissingInputs.Add("Gestor: registrar base explícita (inclusive zero comprovado) e sua origem para prêmio/sobra. Não há distribuição legada automática.");
        result.MissingInputs = result.MissingInputs.Distinct().ToList();
        result.Completeness = applicable.Count == 0 ? PeopleSourceCompleteness.NotConfigured : result.MissingInputs.Count > 0 ? PeopleSourceCompleteness.Incomplete : PeopleSourceCompleteness.Complete;
        var counted = sources.Where(s => s.RuleId != null).ToList();
        result.KnownBasis = counted.Sum(s => s.SignedAmount);
        result.BasisSourceCount = sources.Count;
        result.Rules = applicable.Where(r => (r.EffectiveFrom <= to && r.EffectiveTo >= from) || counted.Any(s => s.RuleId == r.Id)).OrderBy(r => r.EffectiveFrom).ThenBy(r => r.Id).Select(r => r.ToResponse()).ToList();
        if (result.Completeness == PeopleSourceCompleteness.Complete)
        {
            // Sum exact decimal products; round once at the statement boundary, midpoint away from zero.
            result.ProposedAmount = decimal.Round(counted.Sum(s => s.UnroundedCommission ?? 0m), 2, MidpointRounding.AwayFromZero);
            var evidence = operatorId + "|" + from.ToString("yyyyMMdd") + "|" + to.ToString("yyyyMMdd") + "|" + cutoff.ToString("yyyyMMdd") + "|" +
                string.Join(";", result.Rules.Select(r => r.Id)) + "|" + string.Join(";", counted.Select(s => s.SourceId + ":" + s.RuleId + ":" + s.SignedAmount.ToString(CultureInfo.InvariantCulture) + ":" + s.RatePercent?.ToString(CultureInfo.InvariantCulture)));
            result.CalculationId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence)));
        }
        result.Basis = sources.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return result;
    }
}

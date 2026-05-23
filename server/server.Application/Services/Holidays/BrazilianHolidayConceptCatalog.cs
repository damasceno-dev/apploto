using server.Domain.Entities.Enums;

namespace server.Application.Services.Holidays;

public static class BrazilianHolidayConceptCatalog
{
    public const string ConfraternizacaoUniversal = "CONFRATERNIZACAO_UNIVERSAL";
    public const string CarnavalTerca = "CARNAVAL_TERCA";
    public const string QuartaFeiraCinzas = "QUARTA_FEIRA_CINZAS";
    public const string SextaFeiraSanta = "SEXTA_FEIRA_SANTA";
    public const string Tiradentes = "TIRADENTES";
    public const string DiaDoTrabalho = "DIA_DO_TRABALHO";
    public const string CorpusChristi = "CORPUS_CHRISTI";
    public const string Independencia = "INDEPENDENCIA";
    public const string NossaSenhoraAparecida = "NOSSA_SENHORA_APARECIDA";
    public const string Finados = "FINADOS";
    public const string ProclamacaoRepublica = "PROCLAMACAO_REPUBLICA";
    public const string ConscienciaNegra = "CONSCIENCIA_NEGRA";
    public const string Natal = "NATAL";

    public static IReadOnlyList<BrazilianHolidayConcept> All { get; } =
    [
        new(
            ConfraternizacaoUniversal,
            "Confraternização Universal",
            BrazilianHolidayType.National,
            year => new DateOnly(year, 1, 1),
            ["confraternizacao"]),
        new(
            CarnavalTerca,
            "Carnaval (terça)",
            BrazilianHolidayType.OptionalFederal,
            year => BrazilianHolidayEasterCalculator.Compute(year).AddDays(-47),
            ["carnaval"]),
        new(
            QuartaFeiraCinzas,
            "Quarta-feira de Cinzas (até 14h)",
            BrazilianHolidayType.OptionalFederal,
            year => BrazilianHolidayEasterCalculator.Compute(year).AddDays(-46),
            ["cinzas", "quarta-feira de cinzas", "ash wednesday"]),
        new(
            SextaFeiraSanta,
            "Sexta-feira Santa",
            BrazilianHolidayType.National,
            year => BrazilianHolidayEasterCalculator.Compute(year).AddDays(-2),
            ["sexta-feira santa", "good friday"]),
        new(
            Tiradentes,
            "Tiradentes",
            BrazilianHolidayType.National,
            year => new DateOnly(year, 4, 21),
            ["tiradentes"]),
        new(
            DiaDoTrabalho,
            "Dia do Trabalho",
            BrazilianHolidayType.National,
            year => new DateOnly(year, 5, 1),
            ["dia do trabalho", "trabalhador", "labour day", "labor day"]),
        new(
            CorpusChristi,
            "Corpus Christi",
            BrazilianHolidayType.OptionalFederal,
            year => BrazilianHolidayEasterCalculator.Compute(year).AddDays(60),
            ["corpus christi"]),
        new(
            Independencia,
            "Independência",
            BrazilianHolidayType.National,
            year => new DateOnly(year, 9, 7),
            ["independencia"]),
        new(
            NossaSenhoraAparecida,
            "Nossa Senhora Aparecida",
            BrazilianHolidayType.National,
            year => new DateOnly(year, 10, 12),
            ["aparecida", "nossa senhora"]),
        new(
            Finados,
            "Finados",
            BrazilianHolidayType.National,
            year => new DateOnly(year, 11, 2),
            ["finados", "all souls"]),
        new(
            ProclamacaoRepublica,
            "Proclamação da República",
            BrazilianHolidayType.National,
            year => new DateOnly(year, 11, 15),
            ["proclamacao", "republica"]),
        new(
            ConscienciaNegra,
            "Consciência Negra",
            BrazilianHolidayType.National,
            year => new DateOnly(year, 11, 20),
            ["consciencia negra"]),
        new(
            Natal,
            "Natal",
            BrazilianHolidayType.National,
            year => new DateOnly(year, 12, 25),
            ["natal", "christmas"])
    ];
}

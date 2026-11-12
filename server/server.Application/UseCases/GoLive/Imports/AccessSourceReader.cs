using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using server.Communication.Requests;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.GoLive.Imports;

internal sealed record AccessSource(string Digest, Dictionary<string, List<Dictionary<string, string>>> Tables)
{
    private readonly Dictionary<string, Dictionary<string, Dictionary<string, string>>> rowsById =
        Tables.ToDictionary(t => t.Key, t => t.Value.ToDictionary(r => r.First().Value, StringComparer.Ordinal), StringComparer.Ordinal);

    internal Dictionary<string, string>? Find(string table, string id) => rowsById.GetValueOrDefault(table)?.GetValueOrDefault(id);
    internal Dictionary<string, string> Row(string table, string id) => rowsById[table][id];
}

internal static class AccessSourceReader
{
    internal const int MaxFileBytes = 16 * 1024 * 1024;
    internal const int MaxTotalBytes = 64 * 1024 * 1024;
    internal const int MaxRows = 100000;
    private static readonly Dictionary<string, string> Schemas = new()
    {
        ["TblCategoria"] = "cat_id cat_nome cat_sinal",
        ["TblClientes"] = "cli_id cli_nome cli_cpf cli_cep cli_endereco cli_telprincipal cli_telsecundario cli_obs cli_email cli_dataRegistro",
        ["TblConfiguracao"] = "Código conf_dtfechamento",
        ["TblContas"] = "con_id con_identificacao con_agencia con_numero con_tipo",
        ["TblEstoque"] = "est_id est_data prod_id est_valor con_id est_dataRegistro",
        ["TblFeriados"] = "Código fer_data fer_descricao",
        ["TblLancamentos"] = "lco_id lco_data lco_valor id_categoria id_tipo lco_descricao id_cliente lco_vencimento lco_data_pagamento id_conta lco_sinal lco_condicao lco_forma_pagamento lco_origem lco_status lco_dataRegistro",
        ["TblLogExclusao"] = "log_id log_dataexclusao log_IDconta log_valor log_IDcategoria log_IDtipoCategoria log_descricao log_status log_dataLancamento log_dataVencimento log_IDcliente",
        ["TblProduto"] = "produto_id produto_nome",
        ["TblRegistroPonto"] = "pon_id con_id pon_data pon_entrada pon_saida pon_status pon_totalHoras pon_saldoHoras",
        ["TblTelefones"] = "tel_id Telefone id_cliente",
        ["TblTipoCategoria"] = "tipo_id tipo_nome id_categoria",
        ["TblUsuario"] = "usu_id usu_nome usu_senha usu_autenticar usu_nivel con_id",
        ["TblItemRel"] = "id_item item_nome item_tipo item_ordem",
        ["TblRelMensal"] = "idrel Id_item rel_valor rel_idconta rel_data",
        ["TblLoteriaEspecial"] = "lot_id lot_nome lot_modalidade lot_data_inicio lot_data_fim lot_premio lot_concurso",
        ["TblMovimentacaoBancoHoras"] = "mov_id con_id mov_data mov_minutos mov_tipo mov_descricao pon_id",
        ["TblVersaoBE"] = "Código VersaoBE Data_criacao",
        ["TblVersaoFE"] = "Código VersaoFE data_criacao data_atualizacao",
    };

    // Byte evidence is independent of its requested interpretation. This bounded check can
    // identify a received source before profile-specific headers or decoding are applied.
    internal static string EvidenceDigest(RequestGoLiveImportJson request)
    {
        if (request.Profile is not "windows" and not "mestre-1.5.0" || request.Encoding is not "cp1252" and not "utf-8-sig"
            || request.Files is null || request.Files.Count is not 13 and not 19 || request.Files.Any(x => x is null)) Invalid();
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var names = new HashSet<string>(StringComparer.Ordinal);
        var total = 0;
        foreach (var file in request.Files!.OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            if (file.Name is null || !file.Name.EndsWith(".csv", StringComparison.Ordinal)) Invalid();
            var table = file.Name![..^4];
            if (!Schemas.ContainsKey(table) || !names.Add(table) || file.Base64 is null
                || file.Base64.Length > (MaxFileBytes + 2) / 3 * 4) Invalid();
            byte[] bytes;
            try { bytes = Convert.FromBase64String(file.Base64!); }
            catch (FormatException) { Invalid(); throw; }
            total = checked(total + bytes.Length);
            if (bytes.Length > MaxFileBytes || total > MaxTotalBytes) Invalid();
            digest.AppendData(Encoding.UTF8.GetBytes(table + "\0"));
            var length = new byte[8];
            System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(length, bytes.Length);
            digest.AppendData(length);
            digest.AppendData(bytes);
        }
        return Convert.ToHexStringLower(digest.GetHashAndReset());
    }

    public static AccessSource Read(RequestGoLiveImportJson request, string? evidenceDigest = null)
    {
        if (request.Profile is not "windows" and not "mestre-1.5.0" || request.Encoding is not "cp1252" and not "utf-8-sig"
            || request.Files is null || request.Files.Count != (request.Profile == "windows" ? 13 : 19) || request.Files.Any(x => x is null))
            Invalid();
        evidenceDigest ??= EvidenceDigest(request);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = request.Encoding == "cp1252" ? Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback) : new UTF8Encoding(false, true);
        var expected = Schemas.Keys.Take(request.Profile == "windows" ? 13 : 19).ToHashSet(StringComparer.Ordinal);
        var tables = new Dictionary<string, List<Dictionary<string, string>>>();
        var total = 0;
        var rowCount = 0;
        foreach (var file in request.Files!.OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            if (file is null || file.Name is null || !file.Name.EndsWith(".csv", StringComparison.Ordinal)) Invalid();
            var table = file!.Name[..^4];
            if (!expected.Remove(table) || file.Base64 is null || file.Base64.Length > (MaxFileBytes + 2) / 3 * 4) Invalid();
            byte[] bytes;
            try { bytes = Convert.FromBase64String(file.Base64!); }
            catch (FormatException) { Invalid(); throw; }
            total = checked(total + bytes.Length);
            if (bytes.Length > MaxFileBytes || total > MaxTotalBytes) Invalid();
            string text;
            try { text = encoding.GetString(bytes).TrimStart('\uFEFF'); }
            catch (DecoderFallbackException) { Invalid(); throw; }
            if (text.Any(c => c < 32 && c is not '\r' and not '\n' and not '\t')) Invalid();
            var records = Csv(text);
            var header = Schemas[table].Split(' ');
            if (table == "TblRegistroPonto" && request.Profile == "mestre-1.5.0") header = [.. header, "pon_totalMinutos", "pon_saldoMinutos"];
            if (records.Count == 0 || !records[0].SequenceEqual(header)) Invalid();
            var ids = new HashSet<string>();
            var rows = new List<Dictionary<string, string>>();
            foreach (var cells in records.Skip(1))
            {
                if (++rowCount > MaxRows || cells.Length != header.Length || !Regex.IsMatch(cells[0], "^[1-9][0-9]{0,9}$") || !ids.Add(cells[0])) Invalid();
                rows.Add(header.Zip(cells).ToDictionary(x => x.First, x => x.Second));
            }
            tables.Add(table, rows);
        }
        if (expected.Count != 0) Invalid();
        if (request.Profile == "mestre-1.5.0" && new[] { "TblVersaoBE", "TblVersaoFE" }.Any(table =>
            tables[table].Count != 1 || tables[table][0][table == "TblVersaoBE" ? "VersaoBE" : "VersaoFE"] != "1.5.0")) Invalid();
        return new AccessSource(evidenceDigest, tables);
    }

    private static List<string[]> Csv(string text)
    {
        var rows = new List<string[]>();
        var cells = new List<string>();
        var value = new StringBuilder();
        bool quoted = false, closed = false;
        for (int i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { value.Append('"'); i++; }
                    else { quoted = false; closed = true; }
                }
                else value.Append(c);
            }
            else if (c == '"')
            {
                if (value.Length != 0 || closed) Invalid();
                quoted = true;
            }
            else if (c == ',' || c == '\r' || c == '\n')
            {
                cells.Add(value.ToString()); value.Clear(); closed = false;
                if (c != ',')
                {
                    rows.Add(cells.ToArray()); cells.Clear();
                    if (rows.Count > MaxRows + 1) Invalid();
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                }
            }
            else
            {
                if (closed) Invalid();
                value.Append(c);
            }
            if (value.Length > 16384 || cells.Count > 30) Invalid();
        }
        if (quoted) Invalid();
        if (value.Length > 0 || cells.Count > 0 || closed) { cells.Add(value.ToString()); rows.Add(cells.ToArray()); }
        return rows;
    }

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Invalid() => throw new OnValidationException([ResourcesErrorMessages.GOLIVE_SOURCE_INVALID]);
}

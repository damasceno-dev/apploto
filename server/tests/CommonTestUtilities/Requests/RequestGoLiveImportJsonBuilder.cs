using System.Text;
using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public static class RequestGoLiveImportJsonBuilder
{
    public static RequestGoLiveImportJson BuildWindows() => new()
    {
        Profile = "windows", Encoding = "utf-8-sig",
        Files = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Access", "windows"), "*.csv")
            .Select(path => new RequestGoLiveSourceFileJson { Name = Path.GetFileName(path), Base64 = Convert.ToBase64String(File.ReadAllBytes(path)) }).ToList()
    };

    public static RequestGoLiveImportJson BuildMestre()
    {
        var request = BuildWindows();
        request.Profile = "mestre-1.5.0";
        var attendance = request.Files.Single(x => x.Name == "TblRegistroPonto.csv");
        var header = Encoding.UTF8.GetString(Convert.FromBase64String(attendance.Base64)).TrimEnd('\r', '\n');
        attendance.Base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(header + ",pon_totalMinutos,pon_saldoMinutos\n"));
        Add(request, "TblItemRel", "id_item,item_nome,item_tipo,item_ordem\n");
        Add(request, "TblRelMensal", "idrel,Id_item,rel_valor,rel_idconta,rel_data\n");
        Add(request, "TblLoteriaEspecial", "lot_id,lot_nome,lot_modalidade,lot_data_inicio,lot_data_fim,lot_premio,lot_concurso\n");
        Add(request, "TblMovimentacaoBancoHoras", "mov_id,con_id,mov_data,mov_minutos,mov_tipo,mov_descricao,pon_id\n");
        Add(request, "TblVersaoBE", "Código,VersaoBE,Data_criacao\n1,1.5.0,2026/11/01 0:00:00\n");
        Add(request, "TblVersaoFE", "Código,VersaoFE,data_criacao,data_atualizacao\n1,1.5.0,2026/11/01 0:00:00,2026/11/01 0:00:00\n");
        return request;
    }

    private static void Add(RequestGoLiveImportJson request, string table, string content) => request.Files.Add(new RequestGoLiveSourceFileJson
    { Name = table + ".csv", Base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)) });

    public static void ReplaceTable(RequestGoLiveImportJson request, string table, string rows)
    {
        var file = request.Files.Single(x => x.Name == table + ".csv");
        var header = Encoding.UTF8.GetString(Convert.FromBase64String(file.Base64)).Split('\n')[0].TrimEnd('\r');
        file.Base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(header + "\n" + rows));
    }
}

using System.Text.Json;
using server.Communication.Requests;

namespace server.Application.UseCases.GoLive.Imports;

/// <summary>External-path replay entry point; no database, customer writes or Access execution.</summary>
public static class AccessReplayTool
{
    public static int Run(string[] args)
    {
        if (args.Length is not 5 and not 6)
        {
            Console.Error.WriteLine("Uso: access-replay <tables> <windows|mestre-1.5.0> <cp1252|utf-8-sig> <novo-diretorio-privado> [mapeamento.json]");
            return 2;
        }
        try
        {
            var source = ResolvePath(args[1]);
            var output = ResolvePath(args[4]);
            // Require output outside both the working checkout and the complete external export tree.
            var export = Path.GetDirectoryName(source)!;
            var checkout = Path.GetFullPath(Directory.GetCurrentDirectory());
            while (!Directory.Exists(Path.Combine(checkout, ".git")) && !File.Exists(Path.Combine(checkout, ".git")) && Directory.GetParent(checkout) is { } parent)
                checkout = parent.FullName;
            checkout = ResolvePath(checkout);
            if (output == checkout || output.StartsWith(checkout + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || output == export || output.StartsWith(export + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || Directory.Exists(output) || File.Exists(output))
                throw new ArgumentException();
            var directory = new DirectoryInfo(source);
            if (!directory.Exists || directory.LinkTarget is not null) throw new ArgumentException();
            var files = directory.GetFileSystemInfos();
            if (files.Length > 19 || files.Any(x => x is not FileInfo || x.LinkTarget is not null)) throw new ArgumentException();
            var request = new RequestGoLiveImportJson { Profile = args[2], Encoding = args[3] };
            var total = 0;
            foreach (var file in files.Cast<FileInfo>())
            {
                using var stream = file.OpenRead();
                using var memory = new MemoryStream();
                var buffer = new byte[8192];
                int n;
                while ((n = stream.Read(buffer)) > 0)
                {
                    total += n;
                    if (memory.Length + n > AccessSourceReader.MaxFileBytes || total > AccessSourceReader.MaxTotalBytes) throw new ArgumentException();
                    memory.Write(buffer, 0, n);
                }
                request.Files.Add(new RequestGoLiveSourceFileJson { Name = file.Name, Base64 = Convert.ToBase64String(memory.ToArray()) });
            }
            var parsed = AccessSourceReader.Read(request);
            var associations = new Dictionary<string, string>();
            var mappingVersion = "source-register-working-defaults-1";
            if (args.Length == 6)
            {
                using var mappingStream = File.OpenRead(args[5]);
                if (mappingStream.Length > AccessSourceReader.MaxFileBytes) throw new ArgumentException();
                using var mapping = JsonDocument.Parse(mappingStream);
                var root = mapping.RootElement;
                if (root.GetProperty("sourceDigest").GetString() != parsed.Digest) throw new ArgumentException();
                mappingVersion = "reviewed-mapping-" + root.GetProperty("mappingRevision").GetInt32();
                foreach (var item in root.GetProperty("resolutions").EnumerateArray())
                    if (item.GetProperty("table").GetString() == "TblContas" && item.GetProperty("action").GetString() == "Map"
                        && item.TryGetProperty("sourceTerminalId", out var terminal) && terminal.ValueKind == JsonValueKind.String)
                    {
                        var sourceId = item.GetProperty("sourceId").GetString()!;
                        var terminalId = terminal.GetString()!;
                        if (!parsed.Tables["TblContas"].Any(x => x["con_id"] == sourceId && x["con_tipo"] == "Fiado")
                            || !parsed.Tables["TblContas"].Any(x => x["con_id"] == terminalId && x["con_tipo"] == "Terminal")
                            || !associations.TryAdd(sourceId, terminalId)) throw new ArgumentException();
                    }
            }
            var days = AccessFinancialReplay.Compare(parsed.Tables, associations);
            Directory.CreateDirectory(output);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(output, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var options = new JsonSerializerOptions { WriteIndented = true };
            using (var stream = new FileStream(Path.Combine(output, "box-days.json"), FileMode.CreateNew, FileAccess.Write))
                JsonSerializer.Serialize(stream, new { ToolVersion = "m10-replay-2", MappingVersion = mappingVersion, parsed.Digest, Days = days }, options);
            using (var stream = new FileStream(Path.Combine(output, "aggregates.json"), FileMode.CreateNew, FileAccess.Write))
                JsonSerializer.Serialize(stream, new
                {
                    ToolVersion = "m10-replay-2",
                    parsed.Digest,
                    Days = days.Count,
                    Comparable = days.Count(x => x.AccessDifference.HasValue && x.AppDifference.HasValue),
                    ExactArithmeticMatches = days.Count(x => x.AccessDifference.HasValue && x.AppDifference.HasValue && x.AccessDifference == x.AppDifference),
                    MissingEvidence = days.GroupBy(x => x.Completeness).ToDictionary(x => x.Key, x => x.Count()),
                    OperationalReadiness = false
                }, options);
            Console.WriteLine("Replay de leitura concluído; evidência privada gravada fora do repositório.");
            return 0;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException or server.Exceptions.Exceptions.ServerException)
        {
            Console.Error.WriteLine("Replay recusado. Confira perfil, codificação, tabelas, limites e um diretório privado novo fora da origem e do checkout.");
            return 2;
        }
    }
    private static string ResolvePath(string path)
    {
        var full = Path.GetFullPath(path);
        var current = Path.GetPathRoot(full)!;
        foreach (var part in full[current.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            var directory = new DirectoryInfo(current);
            if (directory.Exists && directory.LinkTarget is not null)
                current = directory.ResolveLinkTarget(true)?.FullName ?? throw new ArgumentException();
        }
        return Path.GetFullPath(current);
    }

}

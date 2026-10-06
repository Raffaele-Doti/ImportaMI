using System.Globalization;
using System.Text;

namespace ImportaMI.Core.Import;

/// <summary>Scrive il report di un'importazione in CSV, un file per riga.</summary>
public static class ReportWriter
{
    /// <summary>
    /// Salva il report in <c>&lt;folder&gt;/report-aaaaMMgg-hhmmss.csv</c> e ne restituisce il percorso.
    /// La cartella è quella dei dati dell'app (<see cref="AppDataFolder.Reports"/>), mai la destinazione delle foto.
    /// Separatore ";" e BOM UTF-8, così Excel lo apre correttamente anche con le impostazioni italiane.
    /// </summary>
    public static string Save(ImportReport report, string folder)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"report-{report.Started:yyyyMMdd-HHmmss}.csv");
        File.WriteAllText(path, ToCsv(report), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return path;
    }

    public static string ToCsv(ImportReport report)
    {
        var csv = new StringBuilder();
        csv.AppendLine("session;source;destination;outcome;bytes;capture_time;xxhash64;error");
        foreach (var result in report.Results)
        {
            var session = report.Plan.Sessions[result.File.SessionIndex];
            csv.AppendJoin(';',
                Field(session.Name),
                Field(result.File.Source.SourcePath),
                Field(result.FinalPath ?? result.File.TargetPath),
                result.Outcome.ToString(),
                result.File.Source.SizeBytes.ToString(CultureInfo.InvariantCulture),
                result.File.Source.CaptureTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                result.IsSafe ? result.Hash.ToString("x16", CultureInfo.InvariantCulture) : string.Empty,
                Field(result.Error ?? string.Empty));
            csv.AppendLine();
        }
        return csv.ToString();
    }

    private static string Field(string value) =>
        value.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
}

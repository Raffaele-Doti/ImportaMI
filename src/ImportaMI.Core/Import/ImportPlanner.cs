using ImportaMI.Core.Media;
using ImportaMI.Core.Sessions;

namespace ImportaMI.Core.Import;

/// <summary>Una sessione scelta dall'utente, con il nome da dare alla cartella.</summary>
public sealed record SessionChoice(ShootingSession Session, string Name);

/// <summary>Una sessione nel piano di copia.</summary>
/// <param name="Index">Posizione nel piano, usata per collegare file e sessione.</param>
/// <param name="FolderPath">Cartella assoluta della sessione nella destinazione.</param>
/// <param name="ShotsWithoutFiles">Scatti senza file da copiare con i formati scelti (es. solo RAW con "Solo JPEG").</param>
public sealed record PlannedSession(
    int Index,
    ShootingSession Session,
    string Name,
    string FolderPath,
    IReadOnlyList<string> RelativeSegments,
    int FileCount,
    long Bytes,
    int RawFiles,
    int JpegFiles,
    int VideoFiles,
    int ShotsWithoutFiles);

/// <summary>Un file da copiare e il percorso completo in cui finirà.</summary>
/// <param name="ExistingCopy">
/// Copia probabilmente identica già presente nell'archivio: se il confronto completo lo conferma,
/// il file non viene copiato di nuovo.
/// </param>
public sealed record PlannedFile(MediaFile Source, string TargetPath, int SessionIndex, string? ExistingCopy = null);

public sealed class ImportPlan
{
    public required string SourceRoot { get; init; }

    public required string DestinationRoot { get; init; }

    public required IReadOnlyList<PlannedSession> Sessions { get; init; }

    public required IReadOnlyList<PlannedFile> Files { get; init; }

    public long TotalBytes => Files.Sum(f => f.Source.SizeBytes);

    /// <summary>Byte da scrivere davvero: esclusi i file che sono già nell'archivio.</summary>
    public long BytesToCopy => Files.Where(f => f.ExistingCopy is null).Sum(f => f.Source.SizeBytes);

    public int AlreadyInArchive => Files.Count(f => f.ExistingCopy is not null);
}

public static class ImportPlanner
{
    /// <summary>
    /// Calcola dove finirà ogni file. Se due file della sorgente andrebbero nello stesso percorso
    /// (es. IMG_0001.JPG in due cartelle DCIM diverse) il secondo riceve un suffisso " (1)".
    /// Non tocca il disco: i file già presenti nella destinazione si gestiscono durante la copia.
    /// </summary>
    /// <param name="archiveCopies">Copie già trovate nell'archivio, per percorso sorgente (vedi <c>ArchiveIndex.FindCopies</c>).</param>
    public static ImportPlan Create(
        string sourceRoot,
        IReadOnlyList<SessionChoice> sessions,
        ImportOptions options,
        IReadOnlyDictionary<string, string>? archiveCopies = null)
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var plannedSessions = new List<PlannedSession>();
        var files = new List<PlannedFile>();

        for (var index = 0; index < sessions.Count; index++)
        {
            var (session, name) = sessions[index];
            var segments = FolderNaming.RelativeSegments(session.Start, name, options.Layout);
            var folder = Path.Combine([options.DestinationRoot, .. segments]);
            var sessionFiles = new List<PlannedFile>();
            var shotsWithoutFiles = 0;

            foreach (var shot in session.Shots)
            {
                var selected = options.Format.FilesToImport(shot).ToList();
                if (selected.Count == 0)
                    shotsWithoutFiles++;

                foreach (var file in selected)
                {
                    var subfolder = Subfolders.For(file.Kind, options.Subfolders, options.SubfolderNames);
                    var directory = subfolder is null ? folder : Path.Combine(folder, subfolder);
                    var target = FreeName(Path.Combine(directory, file.FileName), taken.Contains);
                    taken.Add(target);
                    var existing = options.SkipArchiveDuplicates && archiveCopies is not null
                        && archiveCopies.TryGetValue(file.SourcePath, out var copy) ? copy : null;
                    sessionFiles.Add(new PlannedFile(file, target, index, existing));
                }
            }

            files.AddRange(sessionFiles);
            plannedSessions.Add(new PlannedSession(
                index,
                session,
                name,
                folder,
                segments,
                sessionFiles.Count,
                sessionFiles.Sum(f => f.Source.SizeBytes),
                sessionFiles.Count(f => f.Source.Kind == MediaKind.Raw),
                sessionFiles.Count(f => f.Source.Kind == MediaKind.Jpeg),
                sessionFiles.Count(f => f.Source.Kind == MediaKind.Video),
                shotsWithoutFiles));
        }

        return new ImportPlan
        {
            SourceRoot = sourceRoot,
            DestinationRoot = options.DestinationRoot,
            Sessions = plannedSessions,
            Files = files,
        };
    }

    /// <summary>Restituisce <paramref name="path"/>, oppure "nome (1).ext", "nome (2).ext"… finché è occupato.</summary>
    public static string FreeName(string path, Func<string, bool> isTaken)
    {
        if (!isTaken(path))
            return path;

        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        for (var i = 1; ; i++)
        {
            var candidate = Path.Combine(directory, $"{stem} ({i}){extension}");
            if (!isTaken(candidate))
                return candidate;
        }
    }
}

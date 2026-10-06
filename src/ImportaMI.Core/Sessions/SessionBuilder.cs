using ImportaMI.Core.Media;

namespace ImportaMI.Core.Sessions;

/// <summary>
/// Costruisce le sessioni a partire dagli scatti da cui ne inizia una nuova.
/// La proposta iniziale è una sessione per giorno; l'utente poi divide e unisce
/// scegliendo le foto, perché nello stesso giorno possono esserci servizi diversi.
/// </summary>
public static class SessionBuilder
{
    /// <summary>Identificativo stabile di uno scatto: il suo primo file.</summary>
    public static string KeyOf(Shot shot) => shot.Files[0].SourcePath;

    /// <summary>Il primo scatto di ogni giorno: la divisione proposta all'apertura.</summary>
    public static HashSet<string> DayStarts(IEnumerable<Shot> shots)
    {
        var starts = new HashSet<string>(StringComparer.Ordinal);
        DateTime? previousDay = null;
        foreach (var shot in shots.OrderBy(s => s.CaptureTime))
        {
            if (previousDay != shot.CaptureTime.Date)
                starts.Add(KeyOf(shot));
            previousDay = shot.CaptureTime.Date;
        }
        return starts;
    }

    /// <summary>
    /// Divide gli scatti, in ordine di orario, in una sessione nuova a ogni scatto contenuto in
    /// <paramref name="starts"/>. Il primo scatto apre sempre una sessione.
    /// </summary>
    public static IReadOnlyList<ShootingSession> Build(IEnumerable<Shot> shots, IReadOnlySet<string> starts)
    {
        var sessions = new List<ShootingSession>();
        var current = new List<Shot>();

        foreach (var shot in shots.OrderBy(s => s.CaptureTime))
        {
            if (current.Count > 0 && starts.Contains(KeyOf(shot)))
            {
                sessions.Add(new ShootingSession(current));
                current = [];
            }
            current.Add(shot);
        }

        if (current.Count > 0)
            sessions.Add(new ShootingSession(current));
        return sessions;
    }
}

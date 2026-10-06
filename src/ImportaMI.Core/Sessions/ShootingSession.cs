using ImportaMI.Core.Media;

namespace ImportaMI.Core.Sessions;

/// <summary>Una sessione di scatto: scatti consecutivi senza pause più lunghe della soglia.</summary>
public sealed class ShootingSession
{
    public ShootingSession(IEnumerable<Shot> shots)
    {
        Shots = shots.OrderBy(s => s.CaptureTime).ToArray();
        if (Shots.Count == 0)
            throw new ArgumentException("Una sessione deve contenere almeno uno scatto.", nameof(shots));
    }

    public IReadOnlyList<Shot> Shots { get; }

    /// <summary>
    /// Identificativo stabile: il primo file della sessione. Quando l'utente cambia la soglia,
    /// la sessione che inizia con lo stesso scatto mantiene la chiave, e quindi il nome scelto.
    /// </summary>
    public string Key => Shots[0].Files[0].SourcePath;

    public DateTime Start => Shots[0].CaptureTime;

    public DateTime End => Shots[^1].CaptureTime;

    public TimeSpan Duration => End - Start;

    public int FileCount => Shots.Sum(s => s.Files.Count);

    public long SizeBytes => Shots.Sum(s => s.SizeBytes);
}

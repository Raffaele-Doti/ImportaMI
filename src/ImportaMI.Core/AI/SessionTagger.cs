using System;
using System.Collections.Generic;
using System.Linq;

namespace ImportaMI.Core.AI;

/// <summary>
/// Risultato dell'analisi AI di una sessione di scatto.
/// </summary>
public sealed record SessionAiAnalysis(
    string SuggestedName,
    IReadOnlyList<SessionTag> Tags,
    int TotalAnalyzedShots,
    bool IsUndetermined = false
);

/// <summary>
/// Aggrega le classificazioni dei singoli scatti di una sessione e propone tag e nome della cartella.
/// </summary>
public static class SessionTagger
{
    public static SessionAiAnalysis Aggregate(IReadOnlyList<IReadOnlyList<CategoryScore>> shotScores)
    {
        var totalShots = shotScores.Count;
        if (totalShots == 0)
        {
            return new SessionAiAnalysis(string.Empty, [], 0, false);
        }

        // Conteggio e confidenza accumulata per ciascuna categoria
        var categoryStats = new Dictionary<string, (int Count, float SumConfidence)>(StringComparer.OrdinalIgnoreCase);

        foreach (var scores in shotScores)
        {
            if (scores.Count == 0)
                continue;

            // Prendi le categorie significative dello scatto (la prima o quelle con confidenza >= 0.35)
            var best = scores[0];
            var relevant = scores.Where(s => s.Confidence >= 0.35f || s.Category == best.Category).Take(2);

            foreach (var s in relevant)
            {
                if (!categoryStats.TryGetValue(s.Category, out var current))
                {
                    current = (0, 0f);
                }
                categoryStats[s.Category] = (current.Count + 1, current.SumConfidence + s.Confidence);
            }
        }

        // Crea i SessionTag ordinati per frequenza e confidenza
        var tags = categoryStats
            .Select(kv => new SessionTag(
                kv.Key,
                kv.Value.Count,
                kv.Value.Count > 0 ? kv.Value.SumConfidence / kv.Value.Count : 0f))
            .OrderByDescending(t => t.ShotCount)
            .ThenByDescending(t => t.AverageConfidence)
            .Take(4)
            .ToList();

        if (tags.Count == 0)
        {
            // Scatti analizzati ma nessuna categoria ha superato la soglia di riconoscimento
            return new SessionAiAnalysis(string.Empty, [], totalShots, IsUndetermined: true);
        }

        var suggestedName = GenerateSuggestedName(tags);

        return new SessionAiAnalysis(suggestedName, tags, totalShots, IsUndetermined: false);
    }

    public static string GenerateSuggestedName(IReadOnlyList<SessionTag> tags)
    {
        if (tags.Count == 0)
            return string.Empty;

        var top = tags[0];

        // Se c'è una seconda categoria molto presente, suggerisci una combinazione intelligente
        if (tags.Count > 1)
        {
            var second = tags[1];
            if (second.ShotCount >= Math.Max(2, (int)(top.ShotCount * 0.35f)))
            {
                var combined = TryGetCombinedName(top.Category, second.Category);
                if (combined is not null)
                    return combined;
            }
        }

        return CategoryToFolderName(top.Category);
    }

    public static string CategoryToFolderName(string category) => category switch
    {
        PhotoCategories.Mountain => "Montagna",
        PhotoCategories.Beach => "Mare",
        PhotoCategories.Sunset => "Tramonto",
        PhotoCategories.Wedding => "Matrimonio",
        PhotoCategories.BirthdayParty => "Festa",
        PhotoCategories.Concert => "Concerto",
        PhotoCategories.Portrait => "Ritratti",
        PhotoCategories.Nature => "Natura",
        PhotoCategories.City => "Città",
        PhotoCategories.Snow => "Neve",
        PhotoCategories.Night => "Notturna",
        PhotoCategories.Sport => "Sport",
        PhotoCategories.Animals => "Animali",
        PhotoCategories.Motors => "Motori",
        PhotoCategories.Food => "Ristorante",
        PhotoCategories.Macro => "Macro",
        _ => category,
    };

    private static string? TryGetCombinedName(string first, string second)
    {
        var pair = (first, second);
        return pair switch
        {
            (PhotoCategories.Beach, PhotoCategories.Sunset) or (PhotoCategories.Sunset, PhotoCategories.Beach) => "Mare al tramonto",
            (PhotoCategories.Mountain, PhotoCategories.Snow) or (PhotoCategories.Snow, PhotoCategories.Mountain) => "Montagna sulla neve",
            (PhotoCategories.Sport, PhotoCategories.Motors) or (PhotoCategories.Motors, PhotoCategories.Sport) => "Gara motori",
            (PhotoCategories.City, PhotoCategories.Night) or (PhotoCategories.Night, PhotoCategories.City) => "Città di notte",
            (PhotoCategories.Nature, PhotoCategories.Animals) or (PhotoCategories.Animals, PhotoCategories.Nature) => "Natura e animali",
            (PhotoCategories.Wedding, PhotoCategories.Portrait) or (PhotoCategories.Portrait, PhotoCategories.Wedding) => "Servizio matrimonio",
            (PhotoCategories.BirthdayParty, PhotoCategories.Food) or (PhotoCategories.Food, PhotoCategories.BirthdayParty) => "Festa e cena",
            (PhotoCategories.Concert, PhotoCategories.Night) or (PhotoCategories.Night, PhotoCategories.Concert) => "Concerto serale",
            _ => null,
        };
    }
}

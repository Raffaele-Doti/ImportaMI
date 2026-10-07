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
    int TotalAnalyzedShots
);

/// <summary>
/// Aggrega le classificazioni dei singoli scatti di una sessione e propone tag e nome della cartella.
/// </summary>
public static class SessionTagger
{
    public static SessionAiAnalysis Aggregate(IReadOnlyList<IReadOnlyList<CategoryScore>> shotScores)
    {
        var totalShots = shotScores.Count(s => s.Count > 0);
        if (totalShots == 0)
        {
            return new SessionAiAnalysis(string.Empty, [], 0);
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

        var suggestedName = GenerateSuggestedName(tags);

        return new SessionAiAnalysis(suggestedName, tags, totalShots);
    }

    private static string GenerateSuggestedName(IReadOnlyList<SessionTag> tags)
    {
        if (tags.Count == 0)
            return string.Empty;

        var top = tags[0].Category;

        return top switch
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
            _ => top,
        };
    }
}

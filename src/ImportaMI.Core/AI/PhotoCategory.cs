using System;
using System.Collections.Generic;

namespace ImportaMI.Core.AI;

/// <summary>Punteggio di classificazione per una categoria.</summary>
public sealed record CategoryScore(string Category, float Confidence);

/// <summary>Tag assegnato a una sessione con conteggio degli scatti corrispondenti.</summary>
public sealed record SessionTag(string Category, int ShotCount, float AverageConfidence)
{
    public string DisplayText => $"{Category} ({ShotCount})";
}

/// <summary>Elenco delle categorie fotografiche riconosciute dall'AI.</summary>
public static class PhotoCategories
{
    public const string Wedding = "Matrimonio";
    public const string BirthdayParty = "Festa e Compleanno";
    public const string Concert = "Concerto";
    public const string Beach = "Mare e Spiaggia";
    public const string Mountain = "Montagna";
    public const string Nature = "Natura e Paesaggi";
    public const string City = "Città e Architettura";
    public const string Sunset = "Tramonto";
    public const string Portrait = "Ritratti";
    public const string Sport = "Sport";
    public const string Animals = "Animali";
    public const string Night = "Fotografia Notturna";
    public const string Snow = "Neve e Inverno";
    public const string Food = "Cibo e Ristorante";
    public const string Motors = "Motori e Auto";
    public const string Macro = "Macro e Dettagli";

    public static readonly IReadOnlyList<string> All =
    [
        Wedding,
        BirthdayParty,
        Concert,
        Beach,
        Mountain,
        Nature,
        City,
        Sunset,
        Portrait,
        Sport,
        Animals,
        Night,
        Snow,
        Food,
        Motors,
        Macro,
    ];
}

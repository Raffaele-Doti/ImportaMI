using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImportaMI.Core.AI;

namespace ImportaMI.App.ViewModels;

/// <summary>
/// Tag/categoria riconosciuta dall'AI per una sessione.
/// Cliccabile dall'utente per applicare la categoria come nome della cartella di sessione.
/// </summary>
public sealed partial class AiTagViewModel : ObservableObject
{
    private readonly Action<string> _apply;

    public AiTagViewModel(SessionTag tag, Action<string> apply)
    {
        _apply = apply;
        Category = tag.Category;
        ShotCount = tag.ShotCount;
        Confidence = tag.AverageConfidence;
        Label = $"🏷️ {tag.Category}";
        CountText = $"({tag.ShotCount})";
        ToolTip = $"{tag.ShotCount} foto riconosciute come «{tag.Category}» · Clicca per impostare come nome della cartella";
    }

    public string Category { get; }
    public int ShotCount { get; }
    public float Confidence { get; }
    public string Label { get; }
    public string CountText { get; }
    public string ToolTip { get; }

    [RelayCommand]
    private void Apply() => _apply(Category);
}

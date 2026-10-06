using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ImportaMI.App.ViewModels;

/// <summary>Un'opzione di un gruppo a scelta singola, mostrata come radio button.</summary>
public partial class Choice : ObservableObject
{
    private readonly Action<Choice> _selected;

    internal Choice(string group, object value, string title, string? detail, Action<Choice> selected)
    {
        Group = group;
        Value = value;
        Title = title;
        Detail = detail;
        _selected = selected;
    }

    public string Group { get; }

    public object Value { get; }

    public string Title { get; }

    public string? Detail { get; }

    public bool HasDetail => Detail is not null;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    partial void OnIsSelectedChanged(bool value)
    {
        if (value)
            _selected(this);
    }
}

/// <summary>Gruppo di opzioni a scelta singola con valore tipizzato.</summary>
public sealed class ChoiceGroup<T> where T : notnull
{
    private readonly Action _changed;

    public ChoiceGroup(T initial, Action changed, params (T Value, string Title, string? Detail)[] options)
    {
        _changed = changed;
        var group = Guid.NewGuid().ToString("N");
        Options = options.Select(o => new Choice(group, o.Value, o.Title, o.Detail, OnSelected)).ToList();
        Value = initial;
        foreach (var option in Options)
            option.IsSelected = Equals(option.Value, initial);
    }

    public IReadOnlyList<Choice> Options { get; }

    public T Value { get; private set; }

    private void OnSelected(Choice selected)
    {
        foreach (var option in Options.Where(o => o != selected))
            option.IsSelected = false;
        if (Equals(Value, (T)selected.Value))
            return;
        Value = (T)selected.Value;
        _changed();
    }
}

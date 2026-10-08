using System;
using CommunityToolkit.Mvvm.ComponentModel;
using ReSerialSync.Services;
using Serilog.Events;

namespace ReSerialSync.Models;

public sealed partial class LogLevelOption : ObservableObject
{
    private readonly LocalizationService _text;
    private readonly string _key;
    private readonly Action _changed;
    public LogEventLevel Level { get; }
    public string Value { get; }
    public string Label => _text[_key];
    [ObservableProperty] private bool _isEnabled;
    public LogLevelOption(LogEventLevel level, string key, LocalizationService text, bool isEnabled, Action changed)
    {
        Level = level; Value = level.ToString(); _key = key; _text = text; _isEnabled = isEnabled; _changed = changed;
        text.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(LocalizationService.Language)) OnPropertyChanged(nameof(Label));
        };
    }
    partial void OnIsEnabledChanged(bool value) => _changed();
}

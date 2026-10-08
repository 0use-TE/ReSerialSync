using CommunityToolkit.Mvvm.ComponentModel;
using ReSerialSync.Services;

namespace ReSerialSync.Models;

public sealed class LocalizedOption : ObservableObject
{
    private readonly LocalizationService _text;
    private readonly string _key;
    public string Value { get; }
    public string Label => _text[_key];
    public LocalizedOption(string value, string key, LocalizationService text)
    {
        Value = value; _key = key; _text = text;
        text.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(LocalizationService.Language)) OnPropertyChanged(nameof(Label));
        };
    }
}

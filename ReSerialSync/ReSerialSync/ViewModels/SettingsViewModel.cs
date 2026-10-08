using CommunityToolkit.Mvvm.ComponentModel;
using ReSerialSync.Services;

namespace ReSerialSync.ViewModels;

public partial class SettingsViewModel(StyleViewModel style, HelpViewModel help, LocalizationService text, AiSettingsViewModel ai) : ViewModelBase
{
    public StyleViewModel Style { get; } = style;
    public AiSettingsViewModel Ai { get; } = ai;
    public HelpViewModel Help { get; } = help;
    public LocalizationService Text { get; } = text;
    [ObservableProperty] private int _selectedSection;
    public ViewModelBase Content => SelectedSection switch { 0 => Style, 2 => Ai, _ => Help };
    partial void OnSelectedSectionChanged(int value) => OnPropertyChanged(nameof(Content));
}

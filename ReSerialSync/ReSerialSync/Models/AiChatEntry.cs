using CommunityToolkit.Mvvm.ComponentModel;
using LiveMarkdown.Avalonia;
using Material.Icons;
using ReSerialSync.Services;

namespace ReSerialSync.Models;

public sealed class AiChatEntry(string role, LocalizationService text) : ObservableObject
{
    public string Role { get; } = role;
    public string Author => text[Role];
    public bool IsUser => Role == "AiYou";
    public bool IsTool => Role == "AiTool";
    public MaterialIconKind Icon => IsUser ? MaterialIconKind.AccountOutline : IsTool ? MaterialIconKind.WrenchOutline : MaterialIconKind.RobotOutline;
    public ObservableStringBuilder Markdown { get; } = new();
    public void RefreshLanguage() => OnPropertyChanged(nameof(Author));
}

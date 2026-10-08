using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using ReSerialSync.Services;

namespace ReSerialSync.ViewModels;

public partial class HelpViewModel(LocalizationService text, NotificationService notifications) : ViewModelBase
{
    public LocalizationService Text { get; } = text;
    public string Website => "https://0use.net";
    public string GitHub => "https://github.com/0use-TE";
    public string Repository => "https://github.com/0use-TE/ReSerialSync";

    [RelayCommand]
    private async Task OpenRepositoryAsync(Control? anchor) => await OpenAsync(anchor, Repository);

    [RelayCommand]
    private async Task OpenWebsiteAsync(Control? anchor) => await OpenAsync(anchor, Website);
    [RelayCommand]
    private async Task OpenGitHubAsync(Control? anchor) => await OpenAsync(anchor, GitHub);
    private async Task OpenAsync(Control? anchor, string url)
    {
        try
        {
            if (anchor is not null && TopLevel.GetTopLevel(anchor) is { } host)
                await host.Launcher.LaunchUriAsync(new Uri(url));
        }
        catch (Exception ex) { notifications.Error(Text["Help"], ex.Message); }
    }
}

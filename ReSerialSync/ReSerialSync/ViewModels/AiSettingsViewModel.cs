using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ReSerialSync.Extensions;
using ReSerialSync.Services;

namespace ReSerialSync.ViewModels;

public partial class AiSettingsViewModel : ViewModelBase, IDisposable
{
    private readonly CompositeDisposable _subscriptions = new();
    public LocalizationService Text { get; }
    [ObservableProperty] private string _address = "";
    [ObservableProperty] private string _model = "";
    [ObservableProperty] private string _apiKey = "";
    public AiSettingsViewModel(SettingsService settings, LocalizationService text)
    {
        Text = text; Address = settings.Current.Ai.Address; Model = settings.Current.Ai.Model;
        ApiKey = settings.Current.Ai.ApiKey;
        _subscriptions.Add(this.ObserveProperties(nameof(Address), nameof(Model), nameof(ApiKey)).Subscribe(_ =>
        {
            settings.Current.Ai.Address = Address; settings.Current.Ai.Model = Model;
            settings.Current.Ai.ApiKey = ApiKey;
            settings.ScheduleSave();
        }));
    }
    public Uri GetEndpoint()
    {
        var address = Address.Trim().TrimEnd('/');
        if (address.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)) address = address[..^17];
        if (string.IsNullOrWhiteSpace(Model) || !Uri.TryCreate(address, UriKind.Absolute, out var endpoint) || endpoint.Scheme is not ("http" or "https"))
            throw new InvalidOperationException(Text["AiConfigureFirst"]);
        return endpoint;
    }
    public void Dispose() => _subscriptions.Dispose();
}

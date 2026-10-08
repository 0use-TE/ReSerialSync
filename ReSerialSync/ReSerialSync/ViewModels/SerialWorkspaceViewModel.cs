using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReSerialSync.Extensions;
using ReSerialSync.Models;
using ReSerialSync.Services;

namespace ReSerialSync.ViewModels;

public partial class SerialWorkspaceViewModel : ViewModelBase, IDisposable
{
    private readonly ISerialPortFactory _factory;
    private readonly SettingsService _settings;
    private readonly AppLogService _log;
    private readonly NotificationService _notifications;
    private readonly Dictionary<string, SerialSessionViewModel> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly CompositeDisposable _subscriptions = new();
    private IDisposable? _scan;
    private bool _disposed;
    private string? _lastRefreshError;
    private readonly string? _preferredPort;
    public LocalizationService Text { get; }
    public ObservableCollection<SerialSessionViewModel> Ports { get; } = [];
    public bool HasPorts => Ports.Count > 0;
    public bool HasSelection => SelectedPort is not null;
    public int ConnectedCount => _sessions.Values.Count(port => port.IsConnected);
    [ObservableProperty] private SerialSessionViewModel? _selectedPort;

    public SerialWorkspaceViewModel(ISerialPortFactory factory, SettingsService settings, AppLogService log,
        LocalizationService text, NotificationService notifications)
    {
        _factory = factory; _settings = settings; _log = log; Text = text; _notifications = notifications;
        _preferredPort = settings.Current.Layout.SelectedPort;
        text.SetLanguage(settings.Current.Layout.Language);
        _subscriptions.Add(Ports.ObserveCollection().Subscribe(_ => OnPropertyChanged(nameof(HasPorts))));
    }

    public async Task InitializeAsync()
    {
        await RefreshPortsCommand.ExecuteAsync(null);
        _scan ??= Observable.Interval(TimeSpan.FromSeconds(3)).ObserveOnUi().Subscribe(async _ =>
        {
            if (!_disposed && !RefreshPortsCommand.IsRunning) await RefreshPortsCommand.ExecuteAsync(null);
        });
        if (!_factory.IsSupported) _notifications.Warning(Text["Ports"], Text["UnsupportedPlatform"]);
    }

    [RelayCommand]
    private async Task RefreshPortsAsync()
    {
        try
        {
            var names = await Task.Run(_factory.GetPortNames);
            if (_disposed) return;
            _lastRefreshError = null;
            var detected = names.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();
            foreach (var name in detected)
                if (!_sessions.ContainsKey(name)) CreateSession(name);
            foreach (var session in _sessions.Values.ToArray())
            {
                session.IsAvailable = detected.Contains(session.PortName, StringComparer.OrdinalIgnoreCase);
                if (!session.IsAvailable) await session.DeviceRemovedAsync();
            }
            if (_disposed) return;
            var wanted = _sessions.Values.Where(s => s.IsAvailable || s.IsConnected || s == SelectedPort).OrderBy(s => s.PortName, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var session in Ports.Where(s => !wanted.Contains(s)).ToArray()) Ports.Remove(session);
            foreach (var session in wanted)
            {
                if (!Ports.Contains(session)) Ports.Add(session);
                var index = wanted.IndexOf(session);
                if (Ports.IndexOf(session) != index) Ports.Move(Ports.IndexOf(session), index);
            }
            if (SelectedPort is null)
                SelectedPort = Ports.FirstOrDefault(s => s.PortName == _preferredPort) ?? Ports.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _log.Logger.Error(ex, "Port discovery failed");
            if (_lastRefreshError != ex.Message) _notifications.Error(Text["RefreshFailed"], ex.Message);
            _lastRefreshError = ex.Message;
        }
    }

    private void CreateSession(string name)
    {
        var profile = _settings.Current.Ports.TryGetValue(name, out var saved) && saved is not null ? saved : new SerialSettings();
        profile.PortName = name;
        var session = new SerialSessionViewModel(_factory.Create(), _settings, _log, Text, _notifications, profile);
        _sessions[name] = session;
        _settings.Current.Ports[name] = session.CaptureSettings();
        _subscriptions.Add(session.ObserveProperties(nameof(SerialSessionViewModel.IsConnected)).Subscribe(_ => OnPropertyChanged(nameof(ConnectedCount))));
    }

    partial void OnSelectedPortChanged(SerialSessionViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        _settings.Current.Layout.SelectedPort = value?.PortName;
        _settings.ScheduleSave();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _scan?.Dispose(); _subscriptions.Dispose();
        foreach (var session in _sessions.Values) { session.SaveSettings(); session.Dispose(); }
    }
}

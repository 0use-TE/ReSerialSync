using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GOZA.Dock;
using Material.Icons;
using ReSerialSync.Extensions;
using ReSerialSync.Models;
using ReSerialSync.Services;

namespace ReSerialSync.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly SettingsService _settings;
    private readonly AppLogService _log;
    private readonly NotificationService _notifications;
    private readonly Dictionary<string, DockTabViewModel> _tabs;
    private readonly CompositeDisposable _subscriptions = new();
    private static readonly string[] CompactHiddenTabs = ["logs", "ai"];
    private bool _restoring;
    private bool _shutdown;
    private DockSelectionSnapshot _selectionSnapshot;
    private LayoutSettings? _layoutBeforeCompact;
    public SerialWorkspaceViewModel Workspace { get; }
    public LocalizationService Text => Workspace.Text;
    public ConnectionViewModel Connection { get; }
    public HelpViewModel Help { get; }
    public SettingsViewModel SettingsPanel { get; }
    public AiViewModel Ai { get; }
    public ObservableCollection<DockTabViewModel> RightTabs { get; } = [];
    [ObservableProperty] private DockTabViewModel? _rightSelected;
    [ObservableProperty] private DockTabStripPlacement _rightPlacement;
    [ObservableProperty] private GridLength _rightWidth = new(340);
    [ObservableProperty] private bool _isCompact;
    public GridLength ActualRightWidth { get => IsCompact ? new GridLength(0) : RightWidth; set { if (!IsCompact) RightWidth = value; } }
    public double RightMinimumWidth => IsCompact ? 0 : 240;
    public double MinimumWindowWidth => IsCompact ? 900 : 1040;
    public string CompactTooltip => Text[IsCompact ? "FullMode" : "CompactMode"];
    public MaterialIconKind CompactIcon => IsCompact ? MaterialIconKind.ArrowExpandHorizontal : MaterialIconKind.ArrowCollapseHorizontal;
    /// <summary>Hosts the full three-column <c>DockShell</c>; null while compact so the shell is destroyed.</summary>
    public MainViewModel? FullLayoutHost => IsCompact ? null : this;
    /// <summary>Hosts the compact two-column <c>DockShell</c>; null in full mode so the shell is destroyed.</summary>
    public MainViewModel? CompactLayoutHost => IsCompact ? this : null;
    partial void OnRightWidthChanged(GridLength value) => OnPropertyChanged(nameof(ActualRightWidth));
    partial void OnIsCompactChanged(bool value)
    {
        if (!_restoring)
        {
            if (value) EnterCompactLayout();
            else ExitCompactLayout();
        }
        NotifyCompactChrome();
        Dispatcher.UIThread.Post(RestoreDockSelections, DispatcherPriority.Loaded);
    }
    private void NotifyCompactChrome()
    {
        OnPropertyChanged(nameof(ActualRightWidth));
        OnPropertyChanged(nameof(RightMinimumWidth));
        OnPropertyChanged(nameof(MinimumWindowWidth));
        OnPropertyChanged(nameof(CompactTooltip));
        OnPropertyChanged(nameof(CompactIcon));
        OnPropertyChanged(nameof(FullLayoutHost));
        OnPropertyChanged(nameof(CompactLayoutHost));
    }
    private void EnterCompactLayout()
    {
        _layoutBeforeCompact ??= SnapshotLayout();
        ApplyCoreFourRegions();
        _selectionSnapshot = new(_tabs["ports"], _tabs["connection"], _tabs["receive"], _tabs["send"],
            ResolveTab(_layoutBeforeCompact.RightSelected) ?? RightTabs.FirstOrDefault());
    }
    private void ExitCompactLayout()
    {
        if (_layoutBeforeCompact is null) return;
        ApplyLayoutCollections(_layoutBeforeCompact);
        _selectionSnapshot = new(
            ResolveTab(_layoutBeforeCompact.LeftSelected),
            ResolveTab(_layoutBeforeCompact.ConfigurationSelected),
            ResolveTab(_layoutBeforeCompact.TopSelected),
            ResolveTab(_layoutBeforeCompact.BottomSelected),
            ResolveTab(_layoutBeforeCompact.RightSelected));
        _layoutBeforeCompact = null;
    }
    private void ApplyCoreFourRegions()
    {
        LeftTabs.Clear(); ConfigurationTabs.Clear(); TopTabs.Clear(); BottomTabs.Clear(); RightTabs.Clear();
        LeftTabs.Add(_tabs["ports"]);
        ConfigurationTabs.Add(_tabs["connection"]);
        TopTabs.Add(_tabs["receive"]);
        BottomTabs.Add(_tabs["send"]);
        foreach (var id in CompactHiddenTabs)
            if (_tabs.TryGetValue(id, out var tab)) RightTabs.Add(tab);
        LeftSelected = _tabs["ports"];
        ConfigurationSelected = _tabs["connection"];
        TopSelected = _tabs["receive"];
        BottomSelected = _tabs["send"];
        RightSelected = ResolveTab(_layoutBeforeCompact?.RightSelected) ?? RightTabs.FirstOrDefault();
    }
    private void RestoreDockSelections()
    {
        RestoreSelection(LeftTabs, _selectionSnapshot.Left, v => LeftSelected = v, LeftSelected);
        RestoreSelection(ConfigurationTabs, _selectionSnapshot.Configuration, v => ConfigurationSelected = v, ConfigurationSelected);
        RestoreSelection(TopTabs, _selectionSnapshot.Top, v => TopSelected = v, TopSelected);
        RestoreSelection(BottomTabs, _selectionSnapshot.Bottom, v => BottomSelected = v, BottomSelected);
        if (!IsCompact)
            RestoreSelection(RightTabs, _selectionSnapshot.Right, v => RightSelected = v, RightSelected);
    }
    private static void RestoreSelection(ObservableCollection<DockTabViewModel> tabs, DockTabViewModel? preferred,
        Action<DockTabViewModel?> assign, DockTabViewModel? current)
    {
        if (preferred is not null && tabs.Contains(preferred)) assign(preferred);
        else if (current is null) assign(tabs.FirstOrDefault());
    }
    private DockTabViewModel? ResolveTab(string? id) => id is not null && _tabs.TryGetValue(id, out var tab) ? tab : null;
    [RelayCommand] private void ToggleCompact() => IsCompact = !IsCompact;
    private readonly record struct DockSelectionSnapshot(
        DockTabViewModel? Left, DockTabViewModel? Configuration, DockTabViewModel? Top, DockTabViewModel? Bottom, DockTabViewModel? Right);
    private void OpenAiSettings() { SettingsPanel.SelectedSection = 2; IsHelpOpen = true; }
    public ObservableCollection<DockTabViewModel> TopTabs { get; } = [];
    public ObservableCollection<DockTabViewModel> BottomTabs { get; } = [];
    public MaterialIconKind ThemeIcon => Theme == "Dark" ? MaterialIconKind.WhiteBalanceSunny : MaterialIconKind.WeatherNight;
    public string ThemeTooltip => Text[Theme == "Dark" ? "LightTheme" : "DarkTheme"];
    public ObservableCollection<DockTabViewModel> LeftTabs { get; } = [];
    public ObservableCollection<DockTabViewModel> ConfigurationTabs { get; } = [];
    [ObservableProperty] private DockTabViewModel? _leftSelected;
    [ObservableProperty] private DockTabViewModel? _configurationSelected;
    [ObservableProperty] private GridLength _portsHeight = new(1, GridUnitType.Star);
    [ObservableProperty] private GridLength _configurationHeight = new(2, GridUnitType.Star);
    [ObservableProperty] private DockTabStripPlacement _leftPlacement;
    [ObservableProperty] private DockTabStripPlacement _configurationPlacement;
    [ObservableProperty] private DockTabViewModel? _topSelected;
    [ObservableProperty] private DockTabViewModel? _bottomSelected;
    [ObservableProperty] private GridLength _leftWidth = new(300);
    [ObservableProperty] private GridLength _contentWidth = new(1, GridUnitType.Star);
    [ObservableProperty] private GridLength _receiveHeight = new(2, GridUnitType.Star);
    [ObservableProperty] private GridLength _sendHeight = new(1, GridUnitType.Star);
    [ObservableProperty] private DockTabStripPlacement _topPlacement;
    [ObservableProperty] private DockTabStripPlacement _bottomPlacement;
    [ObservableProperty] private string _theme = "System";
    [ObservableProperty] private double _windowWidth = 1200;
    [ObservableProperty] private double _windowHeight = 800;
    [ObservableProperty] private WindowState _windowState;
    [ObservableProperty] private bool _isHelpOpen;

    public MainViewModel(SerialWorkspaceViewModel workspace, SettingsService settings, AppLogService log,
        NotificationService notifications, ConnectionViewModel connection, PortsViewModel ports, ReceiveViewModel receive,
        SendViewModel send, LogViewModel logs, HelpViewModel help, SettingsViewModel settingsPanel, AiViewModel ai)
    {
        Workspace = workspace; _settings = settings; _log = log; _notifications = notifications; Connection = connection; Help = help;
        SettingsPanel = settingsPanel; Ai = ai; Ai.OpenSettingsRequested += OpenAiSettings;
        _tabs = new DockTabViewModel[] { ports, connection, receive, send, logs, ai }.ToDictionary(t => t.Id);
        Restore(settings.Current.Layout);
        _subscriptions.Add(Observable.Merge(RightTabs.ObserveCollection(), LeftTabs.ObserveCollection(), ConfigurationTabs.ObserveCollection(), TopTabs.ObserveCollection(), BottomTabs.ObserveCollection(),
            this.ObserveProperties(nameof(RightSelected), nameof(RightPlacement), nameof(RightWidth), nameof(IsCompact), nameof(LeftSelected), nameof(ConfigurationSelected), nameof(PortsHeight), nameof(ConfigurationHeight), nameof(LeftPlacement), nameof(ConfigurationPlacement), nameof(TopSelected), nameof(BottomSelected), nameof(LeftWidth), nameof(ContentWidth),
                nameof(ReceiveHeight), nameof(SendHeight), nameof(TopPlacement), nameof(BottomPlacement), nameof(Theme),
                nameof(WindowWidth), nameof(WindowHeight), nameof(WindowState)),
            Text.ObserveProperties(nameof(LocalizationService.Language)), Workspace.ObserveProperties(nameof(SerialWorkspaceViewModel.SelectedPort)))
            .Where(_ => !_restoring && !_shutdown).Subscribe(_ => { CaptureLayout(); _settings.ScheduleSave(); }));
        _subscriptions.Add(Text.ObserveProperties(nameof(LocalizationService.Language)).Subscribe(_ => { OnPropertyChanged(nameof(ThemeTooltip)); OnPropertyChanged(nameof(CompactTooltip)); }));
        _settings.SaveFailed += OnSaveFailed;
        ApplyTheme();
    }

    private void Restore(LayoutSettings layout)
    {
        _restoring = true;
        _layoutBeforeCompact = null;
        try
        {
            ApplyLayoutCollections(layout);
            RightWidth = ParseLength(layout.RightWidth, new(340)); RightPlacement = Placement(layout.RightPlacement);
            PortsHeight = ParseLength(layout.PortsHeight, new(1, GridUnitType.Star));
            ConfigurationHeight = ParseLength(layout.ConfigurationHeight, new(2, GridUnitType.Star));
            LeftPlacement = Placement(layout.LeftPlacement); ConfigurationPlacement = Placement(layout.ConfigurationPlacement);
            LeftWidth = ParseLength(layout.LeftWidth, new(300)); ContentWidth = ParseLength(layout.ContentWidth, new(1, GridUnitType.Star));
            ReceiveHeight = ParseLength(layout.ReceiveHeight, new(2, GridUnitType.Star)); SendHeight = ParseLength(layout.SendHeight, new(1, GridUnitType.Star));
            TopPlacement = Placement(layout.TopPlacement); BottomPlacement = Placement(layout.BottomPlacement);
            Theme = layout.Theme is "Dark" or "Light" ? layout.Theme : "System";
            WindowWidth = double.IsFinite(layout.WindowWidth) ? Math.Clamp(layout.WindowWidth, 900, 3000) : 1200;
            WindowHeight = double.IsFinite(layout.WindowHeight) ? Math.Clamp(layout.WindowHeight, 600, 2000) : 800;
            WindowState = layout.WindowState == "Maximized" ? WindowState.Maximized : WindowState.Normal;
            IsCompact = layout.IsCompact;
        }
        finally { _restoring = false; }
        if (IsCompact)
        {
            EnterCompactLayout();
            NotifyCompactChrome();
        }
    }

    private void ApplyLayoutCollections(LayoutSettings layout)
    {
        var used = new HashSet<string>();
        // Previous versions put configuration in the sole left panel.
        var hasPortsPanel = (layout.LeftTabs ?? []).Concat(layout.ConfigurationTabs ?? [])
            .Concat(layout.TopTabs ?? []).Concat(layout.BottomTabs ?? []).Concat(layout.RightTabs ?? []).Contains("ports");
        var legacyLeft = !hasPortsPanel && (layout.LeftTabs ?? []).Contains("connection");
        Fill(RightTabs, layout.RightTabs ?? ["logs", "ai"]);
        Fill(LeftTabs, legacyLeft ? ["ports"] : layout.LeftTabs ?? []);
        Fill(ConfigurationTabs, legacyLeft ? ["connection"] : layout.ConfigurationTabs ?? []);
        Fill(TopTabs, layout.TopTabs ?? []); Fill(BottomTabs, layout.BottomTabs ?? []);
        foreach (var tab in _tabs.Values.Where(t => !used.Contains(t.Id)))
            (tab.Id switch { "ports" => LeftTabs, "connection" => ConfigurationTabs, "receive" => TopTabs, "logs" or "ai" => RightTabs, _ => BottomTabs }).Add(tab);
        LeftSelected = LeftTabs.FirstOrDefault(t => t.Id == layout.LeftSelected) ?? LeftTabs.FirstOrDefault();
        ConfigurationSelected = ConfigurationTabs.FirstOrDefault(t => t.Id == layout.ConfigurationSelected) ?? ConfigurationTabs.FirstOrDefault();
        TopSelected = TopTabs.FirstOrDefault(t => t.Id == layout.TopSelected) ?? TopTabs.FirstOrDefault();
        BottomSelected = BottomTabs.FirstOrDefault(t => t.Id == layout.BottomSelected) ?? BottomTabs.FirstOrDefault();
        RightSelected = RightTabs.FirstOrDefault(t => t.Id == layout.RightSelected) ?? RightTabs.FirstOrDefault();
        void Fill(ObservableCollection<DockTabViewModel> target, IEnumerable<string> ids)
        {
            target.Clear();
            foreach (var id in ids)
                if (id is not null && _tabs.TryGetValue(id, out var tab) && used.Add(id)) target.Add(tab);
        }
    }

    private static DockTabStripPlacement Placement(string value) => Enum.TryParse<DockTabStripPlacement>(value, out var placement) && Enum.IsDefined(placement) ? placement : DockTabStripPlacement.Top;
    private static GridLength ParseLength(string value, GridLength fallback)
    {
        try
        {
            var length = GridLength.Parse(value);
            return length.IsAuto || !double.IsFinite(length.Value) || length.Value <= 0 ? fallback : length;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException) { return fallback; }
    }

    private LayoutSettings SnapshotLayout()
    {
        return new LayoutSettings
        {
            RightTabs = RightTabs.Select(t => t.Id).ToList(), RightWidth = RightWidth.ToString(), RightPlacement = RightPlacement.ToString(), RightSelected = RightSelected?.Id, IsCompact = IsCompact,
            LeftWidth = LeftWidth.ToString(), ContentWidth = ContentWidth.ToString(), ReceiveHeight = ReceiveHeight.ToString(), SendHeight = SendHeight.ToString(),
            PortsHeight = PortsHeight.ToString(), ConfigurationHeight = ConfigurationHeight.ToString(),
            LeftSelected = LeftSelected?.Id, ConfigurationSelected = ConfigurationSelected?.Id,
            LeftPlacement = LeftPlacement.ToString(), ConfigurationPlacement = ConfigurationPlacement.ToString(),
            LeftTabs = LeftTabs.Select(t => t.Id).ToList(), ConfigurationTabs = ConfigurationTabs.Select(t => t.Id).ToList(), TopTabs = TopTabs.Select(t => t.Id).ToList(), BottomTabs = BottomTabs.Select(t => t.Id).ToList(),
            TopSelected = TopSelected?.Id, BottomSelected = BottomSelected?.Id,
            TopPlacement = TopPlacement.ToString(), BottomPlacement = BottomPlacement.ToString(),
            Theme = Theme, Language = Text.Language, SelectedPort = Workspace.SelectedPort?.PortName,
            WindowWidth = WindowWidth, WindowHeight = WindowHeight, WindowState = WindowState.ToString()
        };
    }

    private void CaptureLayout()
    {
        var layout = IsCompact && _layoutBeforeCompact is not null ? CloneLayout(_layoutBeforeCompact) : SnapshotLayout();
        layout.IsCompact = IsCompact;
        layout.LeftWidth = LeftWidth.ToString();
        layout.ContentWidth = ContentWidth.ToString();
        layout.ReceiveHeight = ReceiveHeight.ToString();
        layout.SendHeight = SendHeight.ToString();
        layout.PortsHeight = PortsHeight.ToString();
        layout.ConfigurationHeight = ConfigurationHeight.ToString();
        layout.RightWidth = RightWidth.ToString();
        layout.RightPlacement = RightPlacement.ToString();
        layout.LeftPlacement = LeftPlacement.ToString();
        layout.ConfigurationPlacement = ConfigurationPlacement.ToString();
        layout.TopPlacement = TopPlacement.ToString();
        layout.BottomPlacement = BottomPlacement.ToString();
        layout.Theme = Theme;
        layout.Language = Text.Language;
        layout.SelectedPort = Workspace.SelectedPort?.PortName;
        layout.WindowWidth = WindowWidth;
        layout.WindowHeight = WindowHeight;
        layout.WindowState = WindowState.ToString();
        _settings.Current.Layout = layout;
    }

    private static LayoutSettings CloneLayout(LayoutSettings source) => new()
    {
        RightTabs = source.RightTabs?.ToList(), RightWidth = source.RightWidth, RightPlacement = source.RightPlacement, RightSelected = source.RightSelected, IsCompact = source.IsCompact,
        LeftWidth = source.LeftWidth, ContentWidth = source.ContentWidth, ReceiveHeight = source.ReceiveHeight, SendHeight = source.SendHeight,
        PortsHeight = source.PortsHeight, ConfigurationHeight = source.ConfigurationHeight,
        LeftSelected = source.LeftSelected, ConfigurationSelected = source.ConfigurationSelected,
        LeftPlacement = source.LeftPlacement, ConfigurationPlacement = source.ConfigurationPlacement,
        LeftTabs = source.LeftTabs?.ToList() ?? [], ConfigurationTabs = source.ConfigurationTabs?.ToList() ?? [],
        TopTabs = source.TopTabs?.ToList() ?? [], BottomTabs = source.BottomTabs?.ToList() ?? [],
        TopSelected = source.TopSelected, BottomSelected = source.BottomSelected,
        TopPlacement = source.TopPlacement, BottomPlacement = source.BottomPlacement,
        Theme = source.Theme, Language = source.Language, SelectedPort = source.SelectedPort,
        WindowWidth = source.WindowWidth, WindowHeight = source.WindowHeight, WindowState = source.WindowState
    };

    partial void OnThemeChanged(string value)
    {
        ApplyTheme(); OnPropertyChanged(nameof(ThemeIcon)); OnPropertyChanged(nameof(ThemeTooltip));
    }
    private void ApplyTheme()
    {
        if (Application.Current is { } app) app.RequestedThemeVariant = Theme switch { "Light" => ThemeVariant.Light, "Dark" => ThemeVariant.Dark, _ => ThemeVariant.Default };
    }

    [RelayCommand] private async Task InitializeAsync(Control? anchor)
    {
        _notifications.Attach(anchor);
        if (_settings.Error is not null) _notifications.Warning(Text["SettingsRestoreFailed"], _settings.Error);
        if (_log.StorageWarning is not null) _notifications.Warning(Text["Logs"], _log.StorageWarning);
        await Workspace.InitializeAsync();
        _log.Logger.Information("{Message}", Text["AppStarted"]);
    }
    [RelayCommand] private void ToggleTheme(Control? anchor) => Theme = (anchor?.ActualThemeVariant == ThemeVariant.Dark) ? "Light" : "Dark";
    [RelayCommand] private void ToggleLanguage() => Text.SetLanguage(Text.Language == "zh-CN" ? "en-US" : "zh-CN");
    [RelayCommand] private void ShowHelp() { SettingsPanel.SelectedSection = 1; IsHelpOpen = true; }
    [RelayCommand] private void ShowSettings() { SettingsPanel.SelectedSection = 0; IsHelpOpen = true; }
    [RelayCommand] private void CloseHelp() => IsHelpOpen = false;
    [RelayCommand] private void ResetLayout()
    {
        Restore(new LayoutSettings { Theme = Theme, WindowWidth = WindowWidth, WindowHeight = WindowHeight, WindowState = WindowState.ToString() });
        CaptureLayout(); _settings.ScheduleSave();
    }
    private void OnSaveFailed(string error) => _notifications.Error(Text["SettingsFailed"], error);
    [RelayCommand] private void Shutdown()
    {
        if (_shutdown) return;
        _shutdown = true; _subscriptions.Dispose();
        CaptureLayout(); Workspace.Dispose(); _settings.Flush(); _settings.Dispose();
        _settings.SaveFailed -= OnSaveFailed;
        foreach (var tab in _tabs.Values) tab.Dispose();
        Ai.OpenSettingsRequested -= OpenAiSettings; SettingsPanel.Style.Dispose(); SettingsPanel.Ai.Dispose();
        _log.Logger.Information("Application exited"); _log.Dispose();
    }
}

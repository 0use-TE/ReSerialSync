using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReSerialSync.Extensions;
using ReSerialSync.Models;
using ReSerialSync.Services;
using Serilog.Events;

namespace ReSerialSync.ViewModels;

public sealed partial class LogViewModel : DockTabViewModel
{
    private static readonly LogEventLevel[] AllLevels =
    [
        LogEventLevel.Verbose, LogEventLevel.Debug, LogEventLevel.Information,
        LogEventLevel.Warning, LogEventLevel.Error, LogEventLevel.Fatal
    ];
    private readonly SettingsService _settings;
    private readonly CompositeDisposable _subscriptions = new();
    private readonly SerialDisposable _sessionSubscription = new();
    private bool _suppressLevelSave;
    public ObservableCollection<LogLine> FilteredLogs { get; } = [];
    public IReadOnlyList<LogLevelOption> LevelOptions { get; }
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private LogLine? _selectedLine;
    public int TotalCount => Session?.Logs.Count ?? 0;
    public string LevelFilterLabel
    {
        get
        {
            var enabled = LevelOptions.Where(x => x.IsEnabled).ToArray();
            if (enabled.Length == 0) return Text["NoLevels"];
            if (enabled.Length == LevelOptions.Count) return Text["AllLevels"];
            return enabled.Length <= 2
                ? string.Join(", ", enabled.Select(x => x.Label))
                : $"{enabled.Length} {Text["LogLevel"]}";
        }
    }
    public LogViewModel(SerialWorkspaceViewModel workspace, SettingsService settings) : base("logs", "Logs", workspace)
    {
        _settings = settings;
        SearchText = settings.Current.LogFilter.SearchText;
        var enabled = ResolveEnabledLevels(settings.Current.LogFilter);
        LevelOptions =
        [
            new(LogEventLevel.Verbose, "LogVerbose", Text, enabled.Contains(LogEventLevel.Verbose), OnLevelChanged),
            new(LogEventLevel.Debug, "LogDebug", Text, enabled.Contains(LogEventLevel.Debug), OnLevelChanged),
            new(LogEventLevel.Information, "LogInformation", Text, enabled.Contains(LogEventLevel.Information), OnLevelChanged),
            new(LogEventLevel.Warning, "LogWarning", Text, enabled.Contains(LogEventLevel.Warning), OnLevelChanged),
            new(LogEventLevel.Error, "LogError", Text, enabled.Contains(LogEventLevel.Error), OnLevelChanged),
            new(LogEventLevel.Fatal, "LogFatal", Text, enabled.Contains(LogEventLevel.Fatal), OnLevelChanged)
        ];
        _subscriptions.Add(_sessionSubscription);
        _subscriptions.Add(workspace.ObserveProperties(nameof(SerialWorkspaceViewModel.SelectedPort)).Subscribe(_ => AttachSession()));
        _subscriptions.Add(this.ObserveProperties(nameof(SearchText)).Subscribe(_ =>
        {
            settings.Current.LogFilter.SearchText = SearchText;
            settings.ScheduleSave(); Rebuild();
        }));
        _subscriptions.Add(Text.ObserveProperties(nameof(LocalizationService.Language)).Subscribe(_ => OnPropertyChanged(nameof(LevelFilterLabel))));
        AttachSession();
    }
    private static HashSet<LogEventLevel> ResolveEnabledLevels(LogFilterSettings filter)
    {
        if (filter.EnabledLevels is { Count: > 0 } names)
        {
            var set = new HashSet<LogEventLevel>();
            foreach (var name in names)
                if (Enum.TryParse<LogEventLevel>(name, true, out var level)) set.Add(level);
            if (set.Count > 0) return set;
        }
        Enum.TryParse<LogEventLevel>(filter.MinimumLevel, true, out var minimum);
        return AllLevels.Where(level => level >= minimum).ToHashSet();
    }
    private void OnLevelChanged()
    {
        if (_suppressLevelSave) return;
        _settings.Current.LogFilter.EnabledLevels = LevelOptions.Where(x => x.IsEnabled).Select(x => x.Value).ToList();
        _settings.Current.LogFilter.MinimumLevel = LevelOptions.Where(x => x.IsEnabled).Select(x => x.Level)
            .DefaultIfEmpty(LogEventLevel.Verbose).Min().ToString();
        _settings.ScheduleSave();
        OnPropertyChanged(nameof(LevelFilterLabel));
        Rebuild();
    }
    private void AttachSession()
    {
        _sessionSubscription.Disposable = Session?.Logs.ObserveCollection()
            .Throttle(TimeSpan.FromMilliseconds(50)).ObserveOnUi().Subscribe(_ => Rebuild());
        Rebuild();
    }
    private void Rebuild()
    {
        var enabled = LevelOptions.Where(x => x.IsEnabled).Select(x => x.Level).ToHashSet();
        var lines = Session?.Logs.Select(line => new LogLine(line))
            .Where(line => enabled.Contains(line.Level)
                && (string.IsNullOrWhiteSpace(SearchText) || line.Text.Contains(SearchText, StringComparison.OrdinalIgnoreCase)))
            .ToArray() ?? [];
        var selectedText = SelectedLine?.Text;
        FilteredLogs.Clear(); foreach (var line in lines) FilteredLogs.Add(line);
        SelectedLine = Session?.AutoScroll == true ? lines.LastOrDefault() : lines.FirstOrDefault(x => x.Text == selectedText);
        OnPropertyChanged(nameof(TotalCount));
    }
    public void SetEnabledLevels(params LogEventLevel[] levels)
    {
        var enabled = levels.ToHashSet();
        _suppressLevelSave = true;
        try
        {
            foreach (var option in LevelOptions) option.IsEnabled = enabled.Contains(option.Level);
        }
        finally { _suppressLevelSave = false; }
        OnLevelChanged();
    }
    [RelayCommand] private void ScrollFilteredLog(ListBox? list)
    {
        if (Session?.AutoScroll == true && FilteredLogs.Count > 0) list?.ScrollIntoView(FilteredLogs[^1]);
    }
    [RelayCommand] private Task ExportFilteredLogsAsync(Control? anchor) => Session?.ExportLogLinesAsync(anchor, FilteredLogs.Select(x => x.Text)) ?? Task.CompletedTask;
    public override void Dispose() { _subscriptions.Dispose(); base.Dispose(); }
}

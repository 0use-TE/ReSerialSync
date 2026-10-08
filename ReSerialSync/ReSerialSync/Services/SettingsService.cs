using System;
using System.IO;
using System.Text.Json;
using System.Reactive;
using System.Reactive.Subjects;
using ReSerialSync.Extensions;
using ReSerialSync.Models;

namespace ReSerialSync.Services;

public sealed class SettingsService : IDisposable
{
    private readonly AppLogService _log;
    private readonly Subject<Unit> _changes = new();
    private readonly IDisposable _autoSave;
    private bool _dirty;
    public WorkspaceSettings Current { get; private set; } = new();
    public string FilePath => Path.Combine(_log.DataDirectory, "workspace.json");
    public string? Error { get; private set; }
    public event Action<string>? SaveFailed;

    public SettingsService(AppLogService log)
    {
        _log = log;
        _autoSave = _changes.AutoSave(() => { if (_dirty) Flush(); });
        try
        {
            if (File.Exists(FilePath))
                Current = JsonSerializer.Deserialize(File.ReadAllText(FilePath), SettingsJsonContext.Default.WorkspaceSettings) ?? new();
            Current.Layout ??= new();
            Current.Ports ??= new();
            Current.Ai ??= new(); Current.Appearance ??= new(); Current.LogFilter ??= new();
            if (Current.Serial is { } legacy && !string.IsNullOrWhiteSpace(legacy.PortName) && !Current.Ports.ContainsKey(legacy.PortName))
                Current.Ports[legacy.PortName] = legacy;
            Current.Serial = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Error = ex.Message;
            _log.Logger.Warning(ex, "无法恢复工作区设置");
        }
    }

    public void ScheduleSave()
    {
        _dirty = true;
        _changes.OnNext(Unit.Default);
    }

    public void Flush()
    {
        _dirty = false;
        try
        {
            Directory.CreateDirectory(_log.DataDirectory);
            var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(Current, SettingsJsonContext.Default.WorkspaceSettings));
                File.Move(temporary, FilePath, true);
                Error = null;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Error = ex.Message;
            _log.Logger.Error(ex, "无法保存工作区设置");
            SaveFailed?.Invoke(Error);
        }
    }

    public void Dispose()
    {
        _autoSave.Dispose();
        if (_dirty) Flush();
        _changes.Dispose();
    }
}

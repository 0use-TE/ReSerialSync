using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace ReSerialSync.Services;

public sealed record LogRecord(string Text, string? PortName);

public sealed class AppLogService : IDisposable
{
    private readonly Logger _logger;
    private bool _disposed;
    public string DataDirectory { get; }
    public string LogDirectory => Path.Combine(DataDirectory, "logs");
    public string? StorageWarning { get; }
    public ILogger Logger => _logger;
    public event Action<LogRecord>? EntryWritten;

    public AppLogService(string? dataDirectory = null)
    {
        DataDirectory = dataDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReSerialSync");
        var configuration = new LoggerConfiguration().MinimumLevel.Information().WriteTo.Sink(new UiSink(Emit));
        try
        {
            Directory.CreateDirectory(LogDirectory);
            configuration.WriteTo.File(Path.Combine(LogDirectory, "serial-.log"),
                rollingInterval: RollingInterval.Day, fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true, retainedFileCountLimit: 30, shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{Port}] {Message:lj}{NewLine}{Exception}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            StorageWarning = ex.Message;
        }
        _logger = configuration.CreateLogger();
    }

    public void Emit(LogEvent logEvent)
    {
        var port = logEvent.Properties.TryGetValue("Port", out var value) && value is ScalarValue scalar ? scalar.Value as string : null;
        var line = $"{logEvent.Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{logEvent.Level}] [{port}] {logEvent.RenderMessage(CultureInfo.InvariantCulture)}";
        if (logEvent.Exception is not null) line += $" · {logEvent.Exception.Message}";
        EntryWritten?.Invoke(new LogRecord(line, port));
    }

    public IReadOnlyList<string> ReadRecent(string? portName = null)
    {
        try
        {
            var file = Directory.EnumerateFiles(LogDirectory, "serial-*.log").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (file is null) return [];
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var skipped = stream.Length > 128 * 1024;
            if (skipped) stream.Seek(-128 * 1024, SeekOrigin.End);
            using var reader = new StreamReader(stream);
            if (skipped) reader.ReadLine();
            var lines = new Queue<string>();
            while (reader.ReadLine() is { } line)
            {
                if (portName is not null && !line.Contains($"[{portName}]", StringComparison.Ordinal) && !line.Contains("[]", StringComparison.Ordinal)) continue;
                lines.Enqueue(line);
                if (lines.Count > 500) lines.Dequeue();
            }
            return lines.ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Warning(ex, "无法读取历史日志");
            return [];
        }
    }

    // The logger owns its sinks. The UI sink must not own this service's logger.
    private sealed class UiSink(Action<LogEvent> emit) : ILogEventSink
    {
        public void Emit(LogEvent logEvent) => emit(logEvent);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _logger.Dispose();
    }
}

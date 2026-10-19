using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReSerialSync.Models;
using ReSerialSync.Services;
using ReSerialSync.Extensions;
using Serilog;

namespace ReSerialSync.ViewModels;

public partial class SerialSessionViewModel : ViewModelBase, IDisposable
{
    private const int MaxReceiveCharacters = 256 * 1024;
    private const int MaxReceiveRowChars = 1024;
    private readonly ISerialPortService _serial;
    private readonly SettingsService _settings;
    private readonly AppLogService _log;
    private readonly ILogger _logger;
    private readonly NotificationService _notifications;
    private readonly IDisposable _settingsSubscription;
    private readonly IDisposable _languageSubscription;
    private readonly DispatcherTimer _pump;
    private readonly DispatcherTimer _sendTimer;
    private readonly ConcurrentQueue<byte[]> _receiveQueue = new();
    private readonly ConcurrentQueue<string> _logQueue = new();
    private int _queuedBytes;
    private int _queuedLogs;
    private long _receivedTotal;
    private int _receiveGeneration;
    private bool _disposed;
    private bool _handlingFault;
    private Decoder _decoder;
    private readonly ObservableCollection<ReceiveRow> _receiveLines = [];
    private readonly StringBuilder _receiveOpen = new();
    private bool _receiveOpenRow;
    private bool _receivePendingCr;
    private char _receiveLast = '\n';
    private string? _receiveCache = "";

    public ObservableCollection<string> Logs { get; } = [];
    public int[] BaudRates { get; } = [1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200, 230400, 460800, 921600];
    public int[] DataBitsOptions { get; } = [5, 6, 7, 8];
    public string[] Parities { get; } = ["None", "Odd", "Even", "Mark", "Space"];
    public string[] StopBitsOptions { get; } = ["One", "OnePointFive", "Two"];
    public string[] Handshakes { get; } = ["None", "XOnXOff", "RequestToSend", "RequestToSendXOnXOff"];
    public string[] Encodings { get; } = ["UTF-8", "ASCII", "Latin-1"];
    public string[] LineEndings { get; } = ["None", "CR", "LF", "CRLF"];
    public bool IsSupported => _serial.IsSupported;
    public string DataDirectory => _log.DataDirectory;
    public LocalizationService Text { get; }
    public string PortName { get; }
    public bool CanConfigure => !IsConnected && !IsBusy && IsSupported && IsAvailable;
    public bool CanConnect => !IsBusy && IsSupported && (IsAvailable || IsConnected);
    public string ConnectionAction => Text[IsConnected ? "Disconnect" : "Connect"];
    public string StateLabel => Text[IsBusy ? "Connecting" : IsConnected ? "Connected" : IsAvailable ? "Disconnected" : "Unavailable"];
    public string TimerAction => Text[IsAutoSending ? "StopTimer" : "StartTimer"];
    [ObservableProperty] private bool _isCustomBaudRate;
    public LocalizedOption? SelectedBaudRate
    {
        get => Text.BaudRates.FirstOrDefault(option => option.Value == (IsCustomBaudRate ? "Custom" : BaudRate.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        set
        {
            if (value is null) return;
            if (value.Value == "Custom") { IsCustomBaudRate = true; return; }
            if (int.TryParse(value.Value, out var rate) && BaudRates.Contains(rate))
            {
                IsCustomBaudRate = false;
                BaudRate = rate;
            }
        }
    }
    partial void OnIsCustomBaudRateChanged(bool value) => OnPropertyChanged(nameof(SelectedBaudRate));
    partial void OnBaudRateChanged(int value)
    {
        if (!BaudRates.Contains(value)) IsCustomBaudRate = true;
        OnPropertyChanged(nameof(SelectedBaudRate));
    }
    public string SendPlaceholder => Text[SendHex ? "HexPlaceholder" : "SendPlaceholder"];
    public LocalizedOption? SelectedParity { get => Text.Parities.FirstOrDefault(o => o.Value == Parity); set { if (value is not null) Parity = value.Value; } }
    public LocalizedOption? SelectedStopBits { get => Text.StopBits.FirstOrDefault(o => o.Value == StopBits); set { if (value is not null) StopBits = value.Value; } }
    public LocalizedOption? SelectedHandshake { get => Text.Handshakes.FirstOrDefault(o => o.Value == Handshake); set { if (value is not null) Handshake = value.Value; } }
    public LocalizedOption? SelectedLineEnding { get => Text.LineEndings.FirstOrDefault(o => o.Value == LineEnding); set { if (value is not null) LineEnding = value.Value; } }

    [ObservableProperty] private bool _isAvailable;
    [ObservableProperty] private int _baudRate = 115200;
    [ObservableProperty] private int _dataBits = 8;
    [ObservableProperty] private string _parity = "None";
    [ObservableProperty] private string _stopBits = "One";
    [ObservableProperty] private string _handshake = "None";
    [ObservableProperty] private bool _dtrEnable;
    [ObservableProperty] private bool _rtsEnable;
    [ObservableProperty] private string _encodingName = "UTF-8";
    [ObservableProperty] private bool _sendHex;
    [ObservableProperty] private bool _receiveHex;
    [ObservableProperty] private bool _showTimestamp = true;
    [ObservableProperty] private string _lineEnding = "None";
    [ObservableProperty] private string _sendText = "";
    [ObservableProperty] private int _intervalMs = 1000;
    [ObservableProperty] private bool _autoScroll = true;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isAutoSending;
    [ObservableProperty] private bool _isReceivePaused;
    [ObservableProperty] private long _receivedBytes;
    [ObservableProperty] private long _sentBytes;
    [ObservableProperty] private string? _selectedLog;
    public ObservableCollection<ReceiveRow> ReceiveLines => _receiveLines;
    public bool ReceiveTailFollowed { get; private set; }
    public event EventHandler? ReceiveAppended;
    public string ReceiveText
    {
        get
        {
            if (_receiveCache is not null) return _receiveCache;
            var text = new StringBuilder();
            foreach (var row in _receiveLines)
            {
                text.Append(row.Text);
                text.Append(row.Ending);
            }
            if (_receivePendingCr) text.Append('\r');
            return _receiveCache = text.ToString();
        }
    }

    public SerialSessionViewModel(ISerialPortService serial, SettingsService settings, AppLogService log,
        LocalizationService text, NotificationService notifications, SerialSettings profile)
    {
        _serial = serial; _settings = settings; _log = log; Text = text; _notifications = notifications;
        var s = profile;
        PortName = s.PortName;
        _logger = log.Logger.ForContext("Port", PortName);
        BaudRate = s.BaudRate is > 0 and <= 4000000 ? s.BaudRate : 115200;
        DataBits = DataBitsOptions.Contains(s.DataBits) ? s.DataBits : 8;
        Parity = Parities.Contains(s.Parity) ? s.Parity : "None";
        StopBits = StopBitsOptions.Contains(s.StopBits) ? s.StopBits : "One";
        Handshake = Handshakes.Contains(s.Handshake) ? s.Handshake : "None";
        DtrEnable = s.DtrEnable; RtsEnable = s.RtsEnable;
        EncodingName = Encodings.Contains(s.EncodingName) ? s.EncodingName : "UTF-8";
        SendHex = s.SendHex; ReceiveHex = s.ReceiveHex; ShowTimestamp = s.ShowTimestamp;
        LineEnding = LineEndings.Contains(s.LineEnding) ? s.LineEnding : "None";
        SendText = s.SendText ?? "";
        IntervalMs = Math.Clamp(s.IntervalMs, 20, 3600000); AutoScroll = s.AutoScroll;
        _decoder = SerialCodec.CreateDecoder(EncodingName);
        IsReceivePaused = s.IsReceivePaused;
        _serial.Received += OnReceived; _serial.Faulted += OnFaulted;
        _log.EntryWritten += QueueLog;
        _settingsSubscription = this.ObserveProperties(nameof(BaudRate), nameof(DataBits), nameof(Parity), nameof(StopBits),
            nameof(Handshake), nameof(DtrEnable), nameof(RtsEnable), nameof(EncodingName), nameof(SendHex), nameof(ReceiveHex),
            nameof(ShowTimestamp), nameof(LineEnding), nameof(SendText), nameof(IntervalMs), nameof(AutoScroll), nameof(IsReceivePaused))
            .Subscribe(_ => SaveSettings());
        _languageSubscription = Text.ObserveProperties(nameof(LocalizationService.Language)).Subscribe(_ =>
        {
            UpdateConnectionProperties(); OnPropertyChanged(nameof(TimerAction)); OnPropertyChanged(nameof(SendPlaceholder));
        });
        _pump = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        _pump.Tick += (_, _) => Drain(); _pump.Start();
        _sendTimer = new DispatcherTimer();
        _sendTimer.Tick += async (_, _) =>
        {
            if (SendCommand.IsRunning || !IsConnected || IsBusy) return;
            await SendCommand.ExecuteAsync(null);
        };
    }

    [RelayCommand]
    private async Task ToggleConnectionAsync()
    {
        if (!CanConnect) return;
        IsBusy = true;
        try
        {
            if (IsConnected) await DisconnectAsync();
            else
            {
                if (string.IsNullOrWhiteSpace(PortName)) throw new SerialInputException("PortRequired");
                if (BaudRate is <= 0 or > 4000000) throw new SerialInputException("InvalidBaud");
                _decoder.Reset();
                await _serial.OpenAsync(CaptureSettings());
                IsConnected = true;
                _logger.Information("Connected {Port}, {BaudRate} baud, {DataBits}/{Parity}/{StopBits}, {Handshake}", PortName, BaudRate, DataBits, Parity, StopBits, Handshake);
            }
        }
        catch (Exception ex) { ReportError(ex, "ConnectionFailed"); }
        finally { IsBusy = false; }
    }

    private async Task DisconnectAsync()
    {
        StopAutoSend();
        try { await _serial.CloseAsync(); }
        finally
        {
            IsConnected = false; _decoder.Reset();
            _logger.Information("Disconnected {Port}", PortName);
        }
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        if (!IsConnected || IsBusy) return;
        byte[] bytes;
        try
        {
            bytes = SerialCodec.Encode(SendText, SendHex, EncodingName, LineEnding);
            if (bytes.Length == 0) throw new SerialInputException("EmptyPayload");
        }
        catch (Exception ex) { StopAutoSend(); ReportError(ex, "InvalidPayload"); return; }
        try
        {
            await _serial.WriteAsync(bytes); SentBytes += bytes.Length;
            _logger.Information("TX {Port} {Count} bytes | {Hex}", PortName, bytes.Length, SerialCodec.Hex(bytes));
        }
        catch (Exception ex)
        {
            try { await DisconnectAsync(); } catch (Exception closeError) { _logger.Warning(closeError, "Failed to close port"); }
            ReportError(ex, "SendFailed");
        }
    }

    [RelayCommand]
    private void ToggleAutoSend()
    {
        if (IsAutoSending) { StopAutoSend(); return; }
        if (!IsConnected) { _notifications.Warning(PortName, Text["ConnectFirst"]); return; }
        if (IntervalMs is < 20 or > 3600000) { _notifications.Error(PortName, Text["InvalidInterval"]); return; }
        _sendTimer.Interval = TimeSpan.FromMilliseconds(IntervalMs);
        IsAutoSending = true; _sendTimer.Start();
        _logger.Information("Timer started: {Interval} ms", IntervalMs);
    }

    private void StopAutoSend()
    {
        _sendTimer.Stop();
        if (IsAutoSending) _logger.Information("Timer stopped");
        IsAutoSending = false;
    }

    private void OnReceived(byte[] bytes)
    {
        if (_disposed) return;
        Interlocked.Add(ref _receivedTotal, bytes.Length);
        _logger.Information("RX {Port} {Count} bytes | {Hex}", PortName, bytes.Length, SerialCodec.Hex(bytes));
        if (Interlocked.Add(ref _queuedBytes, bytes.Length) > 1024 * 1024)
        {
            Interlocked.Add(ref _queuedBytes, -bytes.Length); Interlocked.Increment(ref _receiveGeneration);
            return; // Disk logging continues when the bounded display queue is full.
        }
        _receiveQueue.Enqueue(bytes);
    }

    private void OnFaulted(Exception ex) => Dispatcher.UIThread.Post(async () =>
    {
        if (_disposed || _handlingFault) return;
        _handlingFault = true; IsBusy = true;
        try
        {
            try { await DisconnectAsync(); } catch (Exception closeError) { _logger.Warning(closeError, "Failed to close port"); }
            ReportError(ex, "PortFault");
        }
        finally { _handlingFault = false; IsBusy = false; }
    });

    private void QueueLog(LogRecord record)
    {
        if (record.PortName is not null && record.PortName != PortName) return;
        var line = record.Text;
        if (line.Length > 4096) line = line[..4096] + " …";
        if (Interlocked.Increment(ref _queuedLogs) <= 2000) _logQueue.Enqueue(line);
        else Interlocked.Decrement(ref _queuedLogs);
    }

    private void Drain()
    {
        ReceivedBytes = Interlocked.Read(ref _receivedTotal);
        if (Interlocked.Exchange(ref _receiveGeneration, 0) > 0)
        {
            _decoder.Reset(); _notifications.Warning(PortName, Text["DisplayOverflow"]);
        }
        var builder = new StringBuilder();
        for (var i = 0; i < 64 && _receiveQueue.TryDequeue(out var bytes); i++)
        {
            Interlocked.Add(ref _queuedBytes, -bytes.Length);
            if (IsReceivePaused) { _decoder.Reset(); continue; }
            if (ReceiveHex)
            {
                if (ShowTimestamp) builder.Append($"[{DateTimeOffset.Now:HH:mm:ss.fff}] RX ");
                builder.AppendLine(SerialCodec.Hex(bytes));
            }
            else
            {
                var chars = new char[bytes.Length * 2 + 4];
                var count = _decoder.GetChars(bytes, 0, bytes.Length, chars, 0, false);
                var start = 0;
                if (ShowTimestamp)
                {
                    // Keep device line endings, including a CRLF split across reads.
                    // Do not create timestamp-only rows for incomplete UTF-8 characters.
                    while (start < count && chars[start] is '\r' or '\n') builder.Append(chars[start++]);
                    if (start < count)
                    {
                        var previous = builder.Length > 0 ? builder[^1] : _receiveLast;
                        if (previous is not ('\r' or '\n')) builder.AppendLine();
                        builder.Append($"[{DateTimeOffset.Now:HH:mm:ss.fff}] RX ");
                    }
                }
                builder.Append(chars, start, count - start);
            }
        }
        if (builder.Length > 0) AppendReceive(builder.ToString());
        for (var i = 0; i < 200 && _logQueue.TryDequeue(out var line); i++)
        {
            Interlocked.Decrement(ref _queuedLogs); Logs.Add(line);
            if (Logs.Count > 1000) Logs.RemoveAt(0);
        }
        if (AutoScroll && Logs.Count > 0) SelectedLog = Logs[^1];
    }

    [RelayCommand] private void ClearReceive()
    {
        while (_receiveQueue.TryDequeue(out var bytes)) Interlocked.Add(ref _queuedBytes, -bytes.Length);
        _receiveLines.Clear();
        _receiveOpen.Clear();
        _receiveOpenRow = false;
        _receivePendingCr = false;
        _receiveLast = '\n';
        _receiveCache = "";
        ReceiveTailFollowed = false;
        _decoder.Reset();
    }
    [RelayCommand] private void ClearLogs()
    {
        while (_logQueue.TryDequeue(out _)) Interlocked.Decrement(ref _queuedLogs);
        Logs.Clear(); SelectedLog = null;
    }
    [RelayCommand] private void ResetCounters() { Interlocked.Exchange(ref _receivedTotal, 0); ReceivedBytes = 0; SentBytes = 0; }
    [RelayCommand] private void ScrollReceive(ListBox? box)
    {
        if (!AutoScroll || box is null || ReceiveLines.Count == 0) return;
        if (box.SelectedItems is { Count: > 0 }) return;
        box.ScrollIntoView(ReceiveLines[^1]);
        ReceiveTailFollowed = true;
    }

    private void AppendReceive(string addition)
    {
        if (addition.Length == 0) return;
        ReceiveTailFollowed = false;
        _receiveCache = null;
        for (var i = 0; i < addition.Length; i++)
        {
            var c = addition[i];
            if (_receivePendingCr)
            {
                _receivePendingCr = false;
                if (c == '\n')
                {
                    CommitReceiveRow("\r\n");
                    _receiveLast = '\n';
                    continue;
                }
                CommitReceiveRow("\r");
            }
            if (c == '\r')
            {
                _receivePendingCr = true;
                _receiveLast = '\r';
                continue;
            }
            if (c == '\n')
            {
                CommitReceiveRow("\n");
                _receiveLast = '\n';
                continue;
            }
            _receiveOpen.Append(c);
            _receiveLast = c;
            if (_receiveOpen.Length >= MaxReceiveRowChars) CommitReceiveRow("");
        }
        if (_receiveOpen.Length > 0) ShowOpenReceiveRow();
        TrimReceive();
        ReceiveAppended?.Invoke(this, EventArgs.Empty);
    }

    private void CommitReceiveRow(string ending)
    {
        var text = _receiveOpen.ToString();
        _receiveOpen.Clear();
        if (_receiveOpenRow)
        {
            var row = _receiveLines[^1];
            row.Text = text;
            row.Ending = ending;
            _receiveOpenRow = false;
            return;
        }
        _receiveLines.Add(new ReceiveRow(text, ending));
    }

    private void ShowOpenReceiveRow()
    {
        var text = _receiveOpen.ToString();
        if (_receiveOpenRow)
        {
            _receiveLines[^1].Text = text;
            return;
        }
        _receiveLines.Add(new ReceiveRow(text, ""));
        _receiveOpenRow = true;
    }

    private void TrimReceive()
    {
        var excess = ReceiveLength() - MaxReceiveCharacters;
        if (excess <= 0) return;
        _receiveCache = null;
        while (excess > 0 && _receiveLines.Count > (_receiveOpenRow ? 1 : 0))
        {
            var row = _receiveLines[0];
            var size = row.Text.Length + row.Ending.Length;
            if (size <= excess)
            {
                excess -= size;
                _receiveLines.RemoveAt(0);
                continue;
            }
            TrimReceiveRow(row, excess);
            excess = 0;
        }
        if (excess > 0 && _receiveOpenRow)
        {
            var row = _receiveLines[0];
            if (row.Text.Length <= excess)
            {
                excess -= row.Text.Length;
                _receiveLines.RemoveAt(0);
                _receiveOpen.Clear();
                _receiveOpenRow = false;
            }
            else
            {
                var trimmed = row.Text[excess..];
                row.Text = trimmed;
                _receiveOpen.Clear();
                _receiveOpen.Append(trimmed);
                excess = 0;
            }
        }
        if (excess > 0 && _receivePendingCr)
        {
            _receivePendingCr = false;
            excess--;
        }
        if (ReceiveLength() == 0) _receiveLast = '\n';
    }

    private static void TrimReceiveRow(ReceiveRow row, int excess)
    {
        if (excess < row.Text.Length)
        {
            row.Text = row.Text[excess..];
            return;
        }
        var rest = excess - row.Text.Length;
        row.Text = "";
        row.Ending = row.Ending[rest..];
    }

    private int ReceiveLength()
    {
        var count = _receivePendingCr ? 1 : 0;
        var limit = _receiveLines.Count - (_receiveOpenRow ? 1 : 0);
        for (var i = 0; i < limit; i++) count += _receiveLines[i].Text.Length + _receiveLines[i].Ending.Length;
        return count + _receiveOpen.Length;
    }
    [RelayCommand] private void ScrollLog(ListBox? box) { if (AutoScroll && SelectedLog is not null) box?.ScrollIntoView(SelectedLog); }
    [RelayCommand] private Task ExportReceiveAsync(Control? anchor) => ExportAsync(anchor, "receive", ReceiveText);
    [RelayCommand] private Task ExportLogsAsync(Control? anchor) => ExportAsync(anchor, "logs", string.Join(Environment.NewLine, Logs));
    public Task ExportLogLinesAsync(Control? anchor, System.Collections.Generic.IEnumerable<string> lines) => ExportAsync(anchor, "logs", string.Join(Environment.NewLine, lines));

    private async Task ExportAsync(Control? anchor, string name, string content)
    {
        try
        {
            var storage = anchor is null ? null : TopLevel.GetTopLevel(anchor)?.StorageProvider;
            if (storage?.CanSave != true) { _notifications.Warning(Text["ExportFailed"], Text["StorageUnsupported"]); return; }
            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = Text["ExportTitle"], SuggestedFileName = $"{PortName}-{name}-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
                DefaultExtension = "txt", FileTypeChoices = [FilePickerFileTypes.TextPlain]
            });
            if (file is null) return;
            await using var stream = await file.OpenWriteAsync();
            if (stream.CanSeek) stream.SetLength(0);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            await writer.WriteAsync(content);
            _notifications.Success(Text["Exported"], file.Name);
        }
        catch (Exception ex) { ReportError(ex, "ExportFailed"); }
    }

    private void ReportError(Exception ex, string action)
    {
        var message = ex is SerialInputException input ? Text[input.ResourceKey] : ex.Message;
        _notifications.Error($"{PortName} · {Text[action]}", message);
        _logger.Error(ex, "{Action}: {Message}", action, message);
    }

    partial void OnIsConnectedChanged(bool value) => UpdateConnectionProperties();
    partial void OnIsBusyChanged(bool value) => UpdateConnectionProperties();
    partial void OnIsAvailableChanged(bool value) => UpdateConnectionProperties();
    partial void OnIsAutoSendingChanged(bool value) => OnPropertyChanged(nameof(TimerAction));
    partial void OnSendHexChanged(bool value) => OnPropertyChanged(nameof(SendPlaceholder));
    partial void OnParityChanged(string value) => OnPropertyChanged(nameof(SelectedParity));
    partial void OnStopBitsChanged(string value) => OnPropertyChanged(nameof(SelectedStopBits));
    partial void OnHandshakeChanged(string value) => OnPropertyChanged(nameof(SelectedHandshake));
    partial void OnLineEndingChanged(string value) => OnPropertyChanged(nameof(SelectedLineEnding));
    private void UpdateConnectionProperties()
    {
        OnPropertyChanged(nameof(CanConfigure)); OnPropertyChanged(nameof(CanConnect)); OnPropertyChanged(nameof(ConnectionAction)); OnPropertyChanged(nameof(StateLabel));
    }
    partial void OnEncodingNameChanged(string value) => _decoder = SerialCodec.CreateDecoder(value);
    partial void OnReceiveHexChanged(bool value) => _decoder?.Reset();
    partial void OnIsReceivePausedChanged(bool value) => _decoder.Reset();
    partial void OnIntervalMsChanged(int value)
    {
        if (_sendTimer is not null && IsAutoSending)
        {
            if (value is >= 20 and <= 3600000) _sendTimer.Interval = TimeSpan.FromMilliseconds(value);
            else StopAutoSend();
        }
    }

    public SerialSettings CaptureSettings() => new()
    {
        PortName = PortName, BaudRate = BaudRate, DataBits = DataBits, Parity = Parity,
        StopBits = StopBits, Handshake = Handshake, DtrEnable = DtrEnable, RtsEnable = RtsEnable,
        EncodingName = EncodingName, SendHex = SendHex, ReceiveHex = ReceiveHex, ShowTimestamp = ShowTimestamp,
        LineEnding = LineEnding, SendText = SendText, IntervalMs = IntervalMs, AutoScroll = AutoScroll, IsReceivePaused = IsReceivePaused
    };

    public void SaveSettings()
    {
        _settings.Current.Ports[PortName] = CaptureSettings(); _settings.ScheduleSave();
    }

    public async Task DeviceRemovedAsync()
    {
        if (!IsConnected || IsBusy) return;
        IsBusy = true;
        try { await DisconnectAsync(); _notifications.Warning(PortName, Text["Unavailable"]); }
        catch (Exception ex) { ReportError(ex, "PortFault"); }
        finally { IsBusy = false; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; StopAutoSend(); _pump.Stop();
        _serial.Received -= OnReceived; _serial.Faulted -= OnFaulted;
        _settingsSubscription.Dispose(); _languageSubscription.Dispose();
        try { _serial.Dispose(); } catch (Exception ex) { _logger.Warning(ex, "Failed to close port on exit"); }
        _log.EntryWritten -= QueueLog;
    }
}

using System;
using System.IO.Ports;
using System.Threading.Tasks;
using ReSerialSync.Models;

namespace ReSerialSync.Services;

public interface ISerialPortService : IDisposable
{
    bool IsSupported { get; }
    string[] GetPortNames();
    event Action<byte[]>? Received;
    event Action<Exception>? Faulted;
    Task OpenAsync(SerialSettings settings);
    Task CloseAsync();
    Task WriteAsync(byte[] bytes);
}

public sealed class SerialPortService : ISerialPortService
{
    private readonly object _gate = new();
    private SerialPort? _port;
    public bool IsSupported => OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();
    public event Action<byte[]>? Received;
    public event Action<Exception>? Faulted;

    public string[] GetPortNames() => IsSupported ? SerialPort.GetPortNames() : [];

    public Task OpenAsync(SerialSettings settings) => Task.Run(() =>
    {
        if (!IsSupported) throw new SerialInputException("UnsupportedPlatform");
        lock (_gate)
        {
            if (_port is not null) throw new SerialInputException("Connected");
            var port = new SerialPort(settings.PortName, settings.BaudRate,
                Enum.Parse<Parity>(settings.Parity), settings.DataBits, Enum.Parse<StopBits>(settings.StopBits))
            {
                Handshake = Enum.Parse<Handshake>(settings.Handshake),
                DtrEnable = settings.DtrEnable, RtsEnable = settings.RtsEnable,
                ReadTimeout = 300, WriteTimeout = 1000
            };
            try
            {
                port.Open();
                _port = port;
                port.DataReceived += OnReceived;
                port.ErrorReceived += OnError;
                // Bytes can arrive between Open and subscribing to DataReceived.
                if (port.BytesToRead > 0) Task.Run(() => OnReceived(port, null!));
            }
            catch { port.Dispose(); _port = null; throw; }
        }
    });

    private void OnReceived(object sender, SerialDataReceivedEventArgs args)
    {
        try
        {
            lock (_gate)
            {
                if (sender is not SerialPort port || port != _port || !port.IsOpen) return;
                while (port.BytesToRead > 0)
                {
                    var bytes = new byte[Math.Min(port.BytesToRead, 16384)];
                    var count = port.Read(bytes, 0, bytes.Length);
                    if (count == 0) break;
                    if (count != bytes.Length) Array.Resize(ref bytes, count);
                    Received?.Invoke(bytes);
                }
            }
        }
        catch (Exception ex) { Faulted?.Invoke(ex); }
    }

    private void OnError(object sender, SerialErrorReceivedEventArgs args) =>
        Faulted?.Invoke(new InvalidOperationException(args.EventType.ToString()));

    public Task WriteAsync(byte[] bytes) => Task.Run(() =>
    {
        lock (_gate)
        {
            if (_port?.IsOpen != true) throw new SerialInputException("ConnectFirst");
            _port.Write(bytes, 0, bytes.Length);
        }
    });

    public Task CloseAsync() => Task.Run(Dispose);

    public void Dispose()
    {
        SerialPort? port;
        lock (_gate)
        {
            port = _port;
            _port = null;
            if (port is null) return;
            port.DataReceived -= OnReceived;
            port.ErrorReceived -= OnError;
        }
        // SerialPort waits for its event loop on Close: never hold _gate while closing.
        port.Dispose();
    }
}

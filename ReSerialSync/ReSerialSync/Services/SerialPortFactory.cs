namespace ReSerialSync.Services;

public interface ISerialPortFactory
{
    bool IsSupported { get; }
    string[] GetPortNames();
    ISerialPortService Create();
}

public sealed class SerialPortFactory : ISerialPortFactory
{
    public bool IsSupported => System.OperatingSystem.IsWindows() || System.OperatingSystem.IsLinux() || System.OperatingSystem.IsMacOS();
    public string[] GetPortNames() => IsSupported ? System.IO.Ports.SerialPort.GetPortNames() : [];
    public ISerialPortService Create() => new SerialPortService();
}

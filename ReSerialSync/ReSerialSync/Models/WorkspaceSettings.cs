using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ReSerialSync.Models;

public sealed class WorkspaceSettings
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SerialSettings? Serial { get; set; }
    public Dictionary<string, SerialSettings> Ports { get; set; } = new();
    public LayoutSettings Layout { get; set; } = new();
    public AppearanceSettings Appearance { get; set; } = new();
    public LogFilterSettings LogFilter { get; set; } = new();
    public AiSettings Ai { get; set; } = new();
}

public sealed class AiSettings
{
    public string Address { get; set; } = "https://api.openai.com/v1";
    public string Model { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string Draft { get; set; } = "";
}

public sealed class AppearanceSettings
{
    public bool ClassicHeader { get; set; }
}

public sealed class LogFilterSettings
{
    public string SearchText { get; set; } = "";
    public string MinimumLevel { get; set; } = "Verbose";
    public List<string>? EnabledLevels { get; set; }
}

public sealed class SerialSettings
{
    public string PortName { get; set; } = "";
    public int BaudRate { get; set; } = 115200;
    public int DataBits { get; set; } = 8;
    public string Parity { get; set; } = "None";
    public string StopBits { get; set; } = "One";
    public string Handshake { get; set; } = "None";
    public bool DtrEnable { get; set; }
    public bool RtsEnable { get; set; }
    public string EncodingName { get; set; } = "UTF-8";
    public bool SendHex { get; set; }
    public bool ReceiveHex { get; set; }
    public bool ShowTimestamp { get; set; } = true;
    public string LineEnding { get; set; } = "None";
    public string SendText { get; set; } = "";
    public int IntervalMs { get; set; } = 1000;
    public bool AutoScroll { get; set; } = true;
    public bool IsReceivePaused { get; set; }
}

public sealed class LayoutSettings
{
    public double WindowWidth { get; set; } = 1200;
    public double WindowHeight { get; set; } = 800;
    public string WindowState { get; set; } = "Normal";
    public string LeftWidth { get; set; } = "300";
    public string ContentWidth { get; set; } = "*";
    public string ReceiveHeight { get; set; } = "2*";
    public string SendHeight { get; set; } = "*";
    public string Theme { get; set; } = "System";
    public string Language { get; set; } = "zh-CN";
    public string? SelectedPort { get; set; }
    public string PortsHeight { get; set; } = "*";
    public string ConfigurationHeight { get; set; } = "2*";
    public List<string> LeftTabs { get; set; } = ["ports"];
    public List<string> ConfigurationTabs { get; set; } = ["connection"];
    public List<string> TopTabs { get; set; } = ["receive"];
    public List<string> BottomTabs { get; set; } = ["send"];
    public List<string>? RightTabs { get; set; }
    public string RightWidth { get; set; } = "340";
    public string? RightSelected { get; set; } = "ai";
    public string RightPlacement { get; set; } = "Top";
    public bool IsCompact { get; set; }
    public string? LeftSelected { get; set; } = "ports";
    public string? ConfigurationSelected { get; set; } = "connection";
    public string ConfigurationPlacement { get; set; } = "Top";
    public string? TopSelected { get; set; } = "receive";
    public string? BottomSelected { get; set; } = "send";
    public string LeftPlacement { get; set; } = "Top";
    public string TopPlacement { get; set; } = "Top";
    public string BottomPlacement { get; set; } = "Top";
}

[JsonSerializable(typeof(WorkspaceSettings))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class SettingsJsonContext : JsonSerializerContext;

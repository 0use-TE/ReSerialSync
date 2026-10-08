using System;
using System.Text.RegularExpressions;
using Serilog.Events;

namespace ReSerialSync.Models;

public sealed partial class LogLine
{
    public string Text { get; }
    public string Timestamp { get; }
    public string Message { get; }
    public LogEventLevel Level { get; }
    public string LevelLabel => Level.ToString();
    public bool IsWarning => Level == LogEventLevel.Warning;
    public bool IsError => Level >= LogEventLevel.Error;
    public bool IsInformation => Level == LogEventLevel.Information;
    public bool IsDebug => Level <= LogEventLevel.Debug;
    public LogLine(string text)
    {
        Text = text;
        var match = Header().Match(text);
        Timestamp = match.Success ? match.Groups[1].Value : "";
        Message = match.Success ? match.Groups[3].Value : text;
        var level = match.Groups[2].Value;
        Level = level switch
        {
            "VRB" or "Verbose" => LogEventLevel.Verbose,
            "DBG" or "Debug" => LogEventLevel.Debug,
            "WRN" or "Warning" => LogEventLevel.Warning,
            "ERR" or "Error" => LogEventLevel.Error,
            "FTL" or "Fatal" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2} (\d{2}:\d{2}:\d{2}\.\d{3}) [+-]\d{2}:\d{2} \[(\w+)\] \[[^\]]*\] (.*)$")]
    private static partial Regex Header();
}

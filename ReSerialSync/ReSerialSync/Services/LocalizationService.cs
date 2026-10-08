using System.Globalization;
using System.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using ReSerialSync.Models;

namespace ReSerialSync.Services;

public sealed class LocalizationService : ObservableObject
{
    private static readonly ResourceManager Resources = new("ReSerialSync.Resources.Strings", typeof(LocalizationService).Assembly);
    private CultureInfo _culture = CultureInfo.GetCultureInfo("zh-CN");
    public string Language => _culture.Name;
    public string LanguageButton => Language == "zh-CN" ? "EN" : "中文";
    public string this[string key] => Resources.GetString(key, _culture) ?? key;
    public LocalizedOption[] Parities { get; }
    public LocalizedOption[] StopBits { get; }
    public LocalizedOption[] Handshakes { get; }
    public LocalizedOption[] LineEndings { get; }
    public LocalizedOption[] BaudRates { get; }

    public LocalizationService()
    {
        Parities = [new("None", "None", this), new("Odd", "Odd", this), new("Even", "Even", this), new("Mark", "Mark", this), new("Space", "Space", this)];
        StopBits = [new("One", "One", this), new("OnePointFive", "OnePointFive", this), new("Two", "Two", this)];
        Handshakes = [new("None", "None", this), new("XOnXOff", "SoftwareFlow", this), new("RequestToSend", "HardwareFlow", this), new("RequestToSendXOnXOff", "CombinedFlow", this)];
        LineEndings = [new("None", "None", this), new("CR", "CR", this), new("LF", "LF", this), new("CRLF", "CRLF", this)];
        BaudRates = [new("1200", "1200", this), new("2400", "2400", this), new("4800", "4800", this),
            new("9600", "9600", this), new("19200", "19200", this), new("38400", "38400", this),
            new("57600", "57600", this), new("115200", "115200", this), new("230400", "230400", this),
            new("460800", "460800", this), new("921600", "921600", this), new("Custom", "CustomBaudRate", this)];
    }

    public void SetLanguage(string language)
    {
        _culture = CultureInfo.GetCultureInfo(language == "en" || language == "en-US" ? "en-US" : "zh-CN");
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _culture;
        OnPropertyChanged(nameof(Language));
        OnPropertyChanged(nameof(LanguageButton));
        OnPropertyChanged("Item");
        OnPropertyChanged("Item[]");
    }
}

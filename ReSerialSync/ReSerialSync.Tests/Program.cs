using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReSerialSync;
using ReSerialSync.Models;
using ReSerialSync.Services;
using ReSerialSync.ViewModels;
using ReSerialSync.Views;

try
{
    var output = Path.GetFullPath(args.FirstOrDefault() ?? Path.Combine(Environment.CurrentDirectory, ".build", "verification"));
    Directory.CreateDirectory(output);
    TestApp.DataPath = Path.Combine(output, "data-" + Guid.NewGuid().ToString("N"));
    var checks = 0;
    void Check(bool valid, string name)
    {
        if (!valid) throw new Exception("FAIL: " + name);
        checks++; Console.WriteLine("PASS: " + name);
    }
    void Pump(int milliseconds = 160)
    {
        var frame = new DispatcherFrame();
        using var timer = new System.Threading.Timer(_ => Dispatcher.UIThread.Post(() => frame.Continue = false), null, milliseconds, Timeout.Infinite);
        Dispatcher.UIThread.PushFrame(frame);
    }
    void Run(Task task)
    {
        var until = DateTime.UtcNow.AddSeconds(5);
        while (!task.IsCompleted && DateTime.UtcNow < until) Pump(10);
        if (!task.IsCompleted) throw new TimeoutException();
        task.GetAwaiter().GetResult(); Pump();
    }

    Check(SerialCodec.Encode("01 af\n0D 0a", true, "UTF-8", "None").SequenceEqual(new byte[] { 1, 175, 13, 10 }), "HEX whitespace and case");
    Check(SerialCodec.Encode("中文", false, "UTF-8", "CRLF").SequenceEqual(Encoding.UTF8.GetBytes("中文\r\n")), "UTF-8 and line endings");
    foreach (var input in new[] { "A", "0X01", "GG", "01-02" })
    {
        try { SerialCodec.Encode(input, true, "UTF-8", "None"); throw new Exception("Accepted invalid HEX"); }
        catch (FormatException) { Check(true, "Reject invalid HEX " + input); }
    }

    // Seed the previous single-port format to verify migration and selection restoration.
    Directory.CreateDirectory(TestApp.DataPath);
    File.WriteAllText(Path.Combine(TestApp.DataPath, "workspace.json"),
        "{\"Serial\":{\"PortName\":\"COM1\",\"BaudRate\":9600},\"Layout\":{\"SelectedPort\":\"COM1\"}}");
    AppBuilder.Configure<TestApp>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
    EventToCommandChecks.Run(Check);
    var provider = ((TestApp)Application.Current!).Mvvm.ServiceProvider!;
    var vm = provider.GetRequiredService<MainViewModel>();
    var workspace = vm.Workspace;
    var factory = provider.GetRequiredService<FakeSerialFactory>();
    var settings = provider.GetRequiredService<SettingsService>();
    var log = provider.GetRequiredService<AppLogService>();
    var notifications = provider.GetRequiredService<NotificationService>();
    var shown = new List<Notification>();
    notifications.Shown += shown.Add;
    var window = new MainWindow();
    window.Show(); Pump(350);
    Run(vm.InitializeCommand.ExecutionTask ?? throw new Exception("Startup initialization was not triggered"));
    Check(factory.DiscoveryCount == 1 && workspace.Ports.Count == 2,
        "Startup discovers ports without a manual refresh (including Native AOT)");
    var first = workspace.Ports.First(s => s.PortName == "COM1");
    var second = workspace.Ports.First(s => s.PortName == "COM2");
    var firstSerial = factory.Created[0]; var secondSerial = factory.Created[1];
    Check(ReferenceEquals(window.DataContext, vm), "Crystal shares the workspace with the window");
    Check(window.GetVisualDescendants().OfType<ConnectionView>().Any() && window.GetVisualDescendants().OfType<ReceiveView>().Any() && window.GetVisualDescendants().OfType<SendView>().Any(), "Crystal resolves all panel views");
    Check(workspace.Ports.Select(s => s.PortName).SequenceEqual(new[] { "COM1", "COM2" }), "Loaded command discovers all local ports");
    Check(ReferenceEquals(workspace.SelectedPort, first) && first.BaudRate == 9600, "Legacy single-port configuration is migrated");
    var baudSelector = window.GetVisualDescendants().OfType<ComboBox>().Single(c => c.Name == "BaudRateSelector");
    var customBaudEditor = window.GetVisualDescendants().OfType<NumericUpDown>().Single(c => c.Name == "CustomBaudRateEditor");
    baudSelector.SelectedItem = vm.Text.BaudRates.First(option => option.Value == "115200"); Pump();
    Check(first.BaudRate == 115200 && !customBaudEditor.IsVisible, "Common baud rate selection updates the session without showing an extra editor");
    baudSelector.SelectedItem = vm.Text.BaudRates.First(option => option.Value == "Custom"); Pump();
    customBaudEditor.Value = 123456; Pump();
    Check(first.BaudRate == 123456 && customBaudEditor.IsVisible && first.SelectedBaudRate?.Value == "Custom",
        "Custom baud selection exposes a bounded editor and updates the actual port configuration");
    using (var frame = window.CaptureRenderedFrame()) frame!.Save(Path.Combine(output, "custom-baud.png"));
    using (var customRestored = new SerialSessionViewModel(new FakeSerialPort(), settings, log, vm.Text, notifications, first.CaptureSettings()))
        Check(customRestored.BaudRate == 123456 && customRestored.IsCustomBaudRate,
            "Saved nonstandard baud rates restore in custom mode");
    baudSelector.SelectedItem = vm.Text.BaudRates.First(option => option.Value == "9600"); Pump();
    Check(first.BaudRate == 9600 && !first.IsCustomBaudRate && !customBaudEditor.IsVisible,
        "Switching back to a preset hides the custom editor");
    Check(window.Width == 1200 && window.Height == 800, "Window dimensions restored");
    Check(vm.RightTabs.Select(t => t.Id).SequenceEqual(new[] { "logs", "ai" }) && vm.RightSelected?.Id == "ai", "Right dock defaults to logs and AI");
    Check(first.Logs.Any(line => line.Contains(vm.Text["AppStarted"])), "Startup writes a session log entry");
    Check(vm.FullLayoutHost is not null && vm.CompactLayoutHost is null
        && window.GetVisualDescendants().OfType<GOZA.Dock.Controls.DockShell>().Single().Name == "FullShell"
        && window.GetVisualDescendants().OfType<GOZA.Dock.Controls.DockRegion>().Any(r => r.Name == "RightDock"),
        "Full mode hosts the full DockShell with the right DockRegion");
    var rightSize = vm.RightWidth;
    var logsBeforeCompact = vm.RightTabs.First(t => t.Id == "logs");
    vm.RightTabs.Remove(logsBeforeCompact); vm.TopTabs.Add(logsBeforeCompact);
    vm.ToggleCompactCommand.Execute(null); Pump(200);
    Check(vm.ActualRightWidth.Value == 0 && vm.FullLayoutHost is null && vm.CompactLayoutHost is not null
        && window.GetVisualDescendants().OfType<GOZA.Dock.Controls.DockShell>().Single().Name == "CompactShell"
        && !window.GetVisualDescendants().OfType<GOZA.Dock.Controls.DockRegion>().Any(r => r.Name == "RightDock")
        && vm.LeftTabs.Single().Id == "ports" && vm.ConfigurationTabs.Single().Id == "connection"
        && vm.TopTabs.Single().Id == "receive" && vm.BottomTabs.Single().Id == "send"
        && vm.RightTabs.Select(t => t.Id).SequenceEqual(new[] { "logs", "ai" })
        && !window.GetVisualDescendants().OfType<LogView>().Any() && !window.GetVisualDescendants().OfType<AiView>().Any(),
        "Compact mode keeps four core regions and hides logs/AI everywhere");
    using (var frame = window.CaptureRenderedFrame()) frame!.Save(Path.Combine(output, "compact.png"));
    vm.ToggleCompactCommand.Execute(null); Pump(200);
    var rightDock = window.GetVisualDescendants().OfType<GOZA.Dock.Controls.DockRegion>().First(r => r.Name == "RightDock");
    Check(window.GetVisualDescendants().OfType<GOZA.Dock.Controls.DockShell>().Single().Name == "FullShell"
        && rightDock.IsVisible && vm.RightWidth == rightSize && vm.RightSelected?.Id == "ai"
        && vm.TopTabs.Any(t => t.Id == "logs") && vm.RightTabs.All(t => t.Id != "logs"),
        "Full mode rebuilds the DockShell and restores the pre-compact tab layout");
    Check(vm.LeftTabs.Single().Id == "ports" && vm.ConfigurationTabs.Single().Id == "connection", "Ports and selected configuration occupy separate dock regions");
    // Put logs back on the right for the remaining suite.
    var restoredLogs = vm.TopTabs.First(t => t.Id == "logs"); vm.TopTabs.Remove(restoredLogs); vm.RightTabs.Insert(0, restoredLogs); Pump();
    factory.Names = Enumerable.Range(1, 32).Select(i => "COM" + i).ToArray();
    Run(workspace.RefreshPortsCommand.ExecuteAsync(null));
    var portList = window.GetVisualDescendants().OfType<ListBox>().First(b => b.Name == "PortList");
    Check(portList.Items.Count == 32 && portList.MaxHeight == double.PositiveInfinity, "Many ports use a scrolling list without a fixed height cap");
    workspace.SelectedPort = workspace.Ports.First(p => p.PortName == "COM32"); Pump();
    Check(ReferenceEquals(vm.Connection.Session, workspace.SelectedPort), "Selecting a distant port updates the independent configuration panel");
    using (var frame = window.CaptureRenderedFrame()) frame!.Save(Path.Combine(output, "many-ports.png"));
    workspace.SelectedPort = first;
    factory.Names = ["COM1", "COM2"]; Run(workspace.RefreshPortsCommand.ExecuteAsync(null));
    var grid = window.GetVisualDescendants().OfType<Grid>().First(g => g.ColumnDefinitions.Count == 3 && g.ColumnDefinitions[0].MinWidth == 240);
    grid.ColumnDefinitions[0].SetCurrentValue(ColumnDefinition.WidthProperty, new GridLength(320));
    Check(vm.LeftWidth.Value == 320, "Splitter updates the persisted width binding");
    Check(!window.GetVisualDescendants().OfType<Button>().Any(b => Equals(b.Content, "保存布局")), "No manual save button");

    vm.Theme = "Light"; Pump();
    vm.ToggleThemeCommand.Execute(window); Pump();
    Check(window.ActualThemeVariant == ThemeVariant.Dark, "Theme button switches to dark");
    vm.ToggleThemeCommand.Execute(window); Pump();
    Check(window.ActualThemeVariant == ThemeVariant.Light, "Theme button switches to light");
    vm.ToggleLanguageCommand.Execute(null); Pump();
    Check(vm.Text["Connect"] == "Connect" && window.Title?.Contains("Serial debugger") == true, "Language button updates resources and window title");
    Check(window.GetVisualDescendants().OfType<Button>().Any(b => Equals(b.Content, "Connect")), "Already mounted controls update to English");
    Check(vm.TopTabs.First().Header == "Receive", "Dock headers update to English");
    Check(first.SelectedParity?.Label == "None" && first.SelectedStopBits?.Label == "1", "Configuration option labels are localized without changing wire values");
    foreach (var theme in new[] { "Light", "Dark" })
    {
        vm.Theme = theme; Pump(200);
        Check(window.Background is not null, theme + " background uses theme resources");
        using var frame = window.CaptureRenderedFrame();
        Check(frame is not null, theme + " renders");
        frame!.Save(Path.Combine(output, theme.ToLowerInvariant() + ".png"));
    }
    vm.ShowHelpCommand.Execute(null); Pump();
    Check(vm.IsHelpOpen && window.GetVisualDescendants().OfType<HelpView>().Any(), "README opens the localized guide");
    Check(window.GetVisualDescendants().OfType<Border>().First(b => b.Name == "HelpPanel").Background is not null, "Tutorial panel resolves an opaque theme background");
    var helpPanel = window.GetVisualDescendants().OfType<Border>().First(b => b.Name == "HelpPanel");
    Check(helpPanel.BorderBrush is not null && helpPanel.BorderThickness == new Thickness(1), "Guide has a resolved theme border");
    Check(window.GetVisualDescendants().OfType<Button>().Any(b => Equals(b.Content, vm.Help.Repository)), "Guide directly displays the repository address");
    Check(vm.Help.Text["AuthorName"].Contains("Ouse") && vm.Help.Website == "https://0use.net", "Author information uses the supplied profile");
    using (var frame = window.CaptureRenderedFrame()) frame!.Save(Path.Combine(output, "guide-en.png"));
    vm.ToggleLanguageCommand.Execute(null); Pump();
    Check(window.GetVisualDescendants().OfType<TextBlock>().Any(b => b.Text == "使用教程"), "Open guide switches language immediately");
    Check(first.SelectedParity?.Label == "无" && first.Parity == "None", "Mounted configuration options update to Chinese");
    using (var frame = window.CaptureRenderedFrame()) frame!.Save(Path.Combine(output, "guide-zh.png"));
    vm.CloseHelpCommand.Execute(null); Pump();
    Check(!vm.IsHelpOpen, "Guide closes without affecting connections");
    var ai = vm.Ai;
    var aiSettings = vm.SettingsPanel.Ai;
    var welcome = window.GetVisualDescendants().OfType<StackPanel>().Single(panel => panel.Name == "AiWelcome");
    Check(ai.IsChatEmpty && welcome.IsVisible && window.GetVisualDescendants().OfType<TextBlock>().Any(block => block.Text == vm.Text["AiWelcomeTitle"]),
        "Empty AI chat displays a localized welcome panel");
    ai.UsePromptCommand.Execute(vm.Text["AiSuggestPorts"]);
    Check(ai.Prompt == vm.Text["AiSuggestPorts"] && ai.Messages.Count == 0 && workspace.ConnectedCount == 0,
        "Welcome suggestions fill the prompt without sending a request or connecting a port");
    using (var frame = window.CaptureRenderedFrame()) frame!.Save(Path.Combine(output, "ai-welcome.png"));
    ai.OpenSettingsCommand.Execute(null); Pump();
    Check(vm.SettingsPanel.SelectedSection == 2 && window.GetVisualDescendants().OfType<AiSettingsView>().Any(), "Chat opens API configuration inside settings");
    aiSettings.Address = "http://localhost:1234/v1/chat/completions"; aiSettings.Model = "test-model"; aiSettings.ApiKey = "unit-test-secret";
    Check(aiSettings.GetEndpoint().AbsoluteUri.TrimEnd('/') == "http://localhost:1234/v1", "OpenAI-compatible completion addresses normalize to the API base");
    using (var frame = window.CaptureRenderedFrame()) frame!.Save(Path.Combine(output, "ai-settings.png"));
    vm.CloseHelpCommand.Execute(null); Pump();
    ai.Prompt = "connect-test"; Run(ai.SendCommand.ExecuteAsync(null));
    Check(!ai.IsChatEmpty && !welcome.IsVisible, "Starting a conversation hides the welcome panel");
    var aiClient = provider.GetRequiredService<FakeAiChatClientFactory>().Client!;
    Check(second.IsConnected && ReferenceEquals(workspace.SelectedPort, second) && !first.IsConnected, "DotKernel tool calling connects only the requested serial port");
    Check(aiClient.ToolCount >= 6 && aiClient.SawToolResult && aiClient.LastContext.Contains("COM1") && aiClient.LastContext.Contains("COM2"), "DotKernel receives live scene context and tool results");
    Check(ai.Messages.Any(m => m.IsTool && m.Markdown.ToString().Contains("COM2")) && ai.Messages.Any(m => m.Markdown.ToString().Contains("**Ready**")), "Chat shows streaming Markdown and action results");
    Pump(300);
    Check(window.GetVisualDescendants().OfType<LiveMarkdown.Avalonia.MarkdownRenderer>().Any(), "Community Markdown control renders the integrated chat");
    using (var frame = window.CaptureRenderedFrame()) frame!.Save(Path.Combine(output, "ai-chat.png"));
    ai.Prompt = "cancel-test"; var canceled = ai.SendCommand.ExecuteAsync(null); Pump(); ai.StopCommand.Execute(null); Run(canceled);
    Check(!ai.IsBusy && ai.Messages.Last().Markdown.ToString().Contains(ai.Text["AiStopped"]), "Stop cancels an active model stream");
    ai.Prompt = "failure-test"; Run(ai.SendCommand.ExecuteAsync(null));
    Check(!ai.IsBusy && ai.Prompt == "failure-test" && shown.Last().Type == NotificationType.Error, "AI failures are notified and the prompt is preserved for retry");
    ai.ClearChatCommand.Execute(null);
    Check(ai.Messages.Count == 0, "New chat clears the displayed conversation and kernel history");
    Check(ai.IsChatEmpty && welcome.IsVisible, "New chat restores the welcome panel");
    var plugin = provider.GetRequiredService<SerialAiPlugin>();
    Run(plugin.DisconnectAsync("COM2", CancellationToken.None));
    Run(plugin.ConfigureAsync("COM2", 57600, 8, "None", "One", "None", CancellationToken.None));
    Check(second.BaudRate == 57600 && !second.IsConnected, "AI configuration applies validated port parameters");
    try { Run(plugin.ConfigureAsync("COM2", 57600.5, 8, "None", "One", "None", CancellationToken.None)); throw new Exception("Fractional baud accepted"); }
    catch (ArgumentException) { Check(second.BaudRate == 57600, "AI rejects fractional serial parameters before mutation"); }
    Run(plugin.PrepareSendAsync("COM2", "AA", true, CancellationToken.None));
    Check(second.SendText == "AA" && second.SendHex && secondSerial.Writes == 0, "AI preparation fills the editor without transmitting bytes");
    second.SendHex = false; workspace.SelectedPort = first; ai.Prompt = "saved-draft";
    var shell = window.GetVisualDescendants().OfType<GOZA.Dock.Controls.DockShell>().Single();
    Check(shell.TabStripSize == 40, "Dock tab strip keeps the current 40-pixel layout");
    vm.ShowSettingsCommand.Execute(null); Pump();
    Check(window.GetVisualDescendants().OfType<StyleView>().Any(), "Settings opens appearance with left navigation");
    vm.SettingsPanel.Style.ClassicHeader = true; Pump();
    Check(shell.TabPresentation == GOZA.Dock.DockTabPresentation.ClassicTabs && shell.PanePresentation == GOZA.Dock.DockPanePresentation.ClassicSeams, "Classic headers use square tabs and seamless panels");
    using (var frame = window.CaptureRenderedFrame()) frame!.Save(Path.Combine(output, "settings.png"));
    vm.CloseHelpCommand.Execute(null); Pump();
    using (var frame = window.CaptureRenderedFrame()) frame!.Save(Path.Combine(output, "classic.png"));
    vm.SettingsPanel.Style.ClassicHeader = false; Pump();
    Check(shell.PanePresentation == GOZA.Dock.DockPanePresentation.ModernCards && shell.TabPresentation == GOZA.Dock.DockTabPresentation.ModernPills, "Modern headers use rounded pills and panel cards");
    vm.SettingsPanel.Style.ClassicHeader = true; Pump();
    var logVm = provider.GetRequiredService<LogViewModel>();
    first.Logs.Add("2026-10-03 12:34:56.001 +08:00 [INF] [COM1] filter-info");
    first.Logs.Add("2026-10-03 12:34:56.002 +08:00 [WRN] [COM1] filter-warning");
    first.Logs.Add("2026-10-03 12:34:56.003 +08:00 [Error] [COM1] filter-error");
    logVm.SearchText = "FILTER-";
    logVm.SetEnabledLevels(Serilog.Events.LogEventLevel.Warning, Serilog.Events.LogEventLevel.Error, Serilog.Events.LogEventLevel.Fatal); Pump();
    Check(logVm.FilteredLogs.Count == 2 && logVm.FilteredLogs.All(x => x.Level >= Serilog.Events.LogEventLevel.Warning), "Log search ignores case and combines with selected levels");
    Check(logVm.FilteredLogs[0].IsWarning && logVm.FilteredLogs[1].IsError, "Historical and live severity formats retain distinct log levels");
    var logTab = vm.RightTabs.First(x => x.Id == "logs"); vm.RightSelected = logTab; Pump();
    using (var frame = window.CaptureRenderedFrame()) frame!.Save(Path.Combine(output, "filtered-logs.png"));
    workspace.SelectedPort = second; Pump();
    Check(logVm.FilteredLogs.Count == 0, "Log filters follow the selected port without leaking previous rows");
    workspace.SelectedPort = first; logVm.SearchText = "";
    logVm.SetEnabledLevels(Serilog.Events.LogEventLevel.Verbose, Serilog.Events.LogEventLevel.Debug, Serilog.Events.LogEventLevel.Information, Serilog.Events.LogEventLevel.Warning, Serilog.Events.LogEventLevel.Error, Serilog.Events.LogEventLevel.Fatal);
    vm.SettingsPanel.Style.ClassicHeader = false; vm.BottomSelected = vm.BottomTabs.First(x => x.Id == "send"); Pump();

    var sendBox = window.GetVisualDescendants().OfType<SendView>().Single().GetVisualDescendants().OfType<TextBox>().First(b => b.AcceptsReturn && !b.IsReadOnly);
    var receiveBox = window.GetVisualDescendants().OfType<ReceiveView>().Single().GetVisualDescendants().OfType<TextBox>().First(b => b.IsReadOnly);
    Check(sendBox.Padding == new Thickness(12) && receiveBox.Padding == new Thickness(12), "Both editors have explicit padding");
    Run(first.ToggleConnectionCommand.ExecuteAsync(null));
    Check(first.IsConnected && !first.CanConfigure, "Connection locks only its own parameters");
    workspace.SelectedPort = second; second.BaudRate = 57600;
    Run(second.ToggleConnectionCommand.ExecuteAsync(null));
    Check(first.IsConnected && second.IsConnected && workspace.ConnectedCount == 2, "Multiple ports connect simultaneously");
    first.SendHex = true; first.SendText = "01 FF"; first.LineEnding = "CRLF";
    Run(first.SendCommand.ExecuteAsync(null));
    Check(firstSerial.LastWrite!.SequenceEqual(new byte[] { 1, 255, 13, 10 }) && first.SentBytes == 4, "Each session sends its own bytes and counts");
    second.SendText = "second";
    Run(second.SendCommand.ExecuteAsync(null));
    Check(secondSerial.LastWrite!.SequenceEqual(Encoding.UTF8.GetBytes("second")) && second.SentBytes == 6, "Second session has an independent payload");
    first.SendText = "Z1"; Run(first.SendCommand.ExecuteAsync(null));
    Check(firstSerial.Writes == 1 && shown.Last().Type == NotificationType.Error && shown.Last().Title?.Contains("COM1") == true, "Invalid payload produces an error notification");
    Check(window.GetVisualDescendants().OfType<NotificationCard>().Any(), "Error notification renders in the window");
    vm.ToggleLanguageCommand.Execute(null); Run(first.SendCommand.ExecuteAsync(null));
    Check(shown.Last().Title?.Contains("Invalid payload") == true && shown.Last().Message?.Contains("HEX accepts") == true, "Error titles and validation messages use the selected language");
    vm.ToggleLanguageCommand.Execute(null);

    first.ReceiveHex = false; first.ShowTimestamp = false; second.ShowTimestamp = false;
    var chinese = Encoding.UTF8.GetBytes("中文");
    firstSerial.Receive(chinese[..2]); Pump(); firstSerial.Receive(chinese[2..]); Pump();
    secondSerial.Receive(Encoding.UTF8.GetBytes("other")); Pump();
    Check(first.ReceiveText == "中文" && first.ReceivedBytes == 6, "UTF-8 decoder preserves partial characters");
    Check(second.ReceiveText == "other" && receiveBox.Text == "other", "Inactive port keeps receiving without changing the selected display");
    workspace.SelectedPort = first; Pump();
    Check(receiveBox.Text == "中文" && sendBox.Text == "Z1", "Selecting a port restores its receive and send VM");
    first.IsReceivePaused = true; firstSerial.Receive([0x41]); Pump();
    Check(first.ReceiveText == "中文" && first.ReceivedBytes == 7, "Pause still receives and counts");
    first.IsReceivePaused = false; first.ReceiveHex = true; firstSerial.Receive([0x00, 0xFF]); Pump();
    Check(first.ReceiveText.Contains("00 FF") && receiveBox.CaretIndex == receiveBox.Text!.Length, "HEX display and TextChanged auto-scroll");
    Check(!second.Logs.Any(line => line.Contains("TX COM1")) && !first.Logs.Any(line => line.Contains("TX COM2")), "Per-port log streams remain isolated");

    string WithoutReceiveTimestamps() => System.Text.RegularExpressions.Regex.Replace(first.ReceiveText,
        @"\[\d{2}:\d{2}:\d{2}\.\d{3}\] RX ", "");
    first.ReceiveHex = false; first.ShowTimestamp = true; first.ClearReceiveCommand.Execute(null);
    firstSerial.Receive(Encoding.UTF8.GetBytes("ACK hi\r\n")); Pump();
    firstSerial.Receive(Encoding.UTF8.GetBytes("ACK asd\r\n")); Pump();
    Check(WithoutReceiveTimestamps() == "ACK hi\r\nACK asd\r\n", "Timestamped CRLF replies do not acquire extra blank lines");
    using (var frame = window.CaptureRenderedFrame()) frame!.Save(Path.Combine(output, "receive-spacing.png"));
    first.ClearReceiveCommand.Execute(null);
    firstSerial.Receive(Encoding.UTF8.GetBytes("ACK split\r")); Pump();
    firstSerial.Receive(Encoding.UTF8.GetBytes("\n")); Pump();
    firstSerial.Receive(Encoding.UTF8.GetBytes("ACK next\r\n")); Pump();
    Check(WithoutReceiveTimestamps() == "ACK split\r\nACK next\r\n", "CRLF split between driver reads stays one line ending");
    first.ClearReceiveCommand.Execute(null);
    firstSerial.Receive(Encoding.UTF8.GetBytes("LF\n")); Pump();
    firstSerial.Receive(Encoding.UTF8.GetBytes("CR\r")); Pump();
    Check(WithoutReceiveTimestamps() == "LF\nCR\r", "Existing LF and CR endings are preserved without added spacing");
    first.ClearReceiveCommand.Execute(null);
    firstSerial.Receive(Encoding.UTF8.GetBytes("intentional\r\n\r\n")); Pump();
    Check(WithoutReceiveTimestamps() == "intentional\r\n\r\n", "Blank lines actually sent by the device are retained");
    first.ClearReceiveCommand.Execute(null);
    firstSerial.Receive(Encoding.UTF8.GetBytes("one")); Pump();
    firstSerial.Receive(Encoding.UTF8.GetBytes("two")); Pump();
    Check(WithoutReceiveTimestamps() == "one" + Environment.NewLine + "two", "Unterminated timestamped chunks receive just one separator");
    first.ClearReceiveCommand.Execute(null);
    firstSerial.Receive(chinese[..2]); Pump();
    Check(first.ReceiveText.Length == 0, "Incomplete UTF-8 does not create a timestamp-only line");
    firstSerial.Receive(chinese[2..]); Pump();
    Check(WithoutReceiveTimestamps() == "中文", "Timestamped reception still decodes split UTF-8 correctly");
    first.ReceiveHex = true; first.ShowTimestamp = false;

    first.SendText = "AA"; first.LineEnding = "None"; first.IntervalMs = 20;
    first.ToggleAutoSendCommand.Execute(null);
    workspace.SelectedPort = second; Pump(140);
    Check(firstSerial.Writes > 1 && first.IsAutoSending, "Switching selection preserves background timed sending");
    factory.Names = ["COM2"];
    Pump(3300);
    Check(!first.IsAvailable && !first.IsConnected && !first.IsAutoSending && second.IsConnected,
        "Startup also enables automatic scanning: unplugging closes only that session without manual refresh");
    factory.Names = ["COM1", "COM2"]; Run(workspace.RefreshPortsCommand.ExecuteAsync(null));
    Check(ReferenceEquals(workspace.Ports.First(p => p.PortName == "COM1"), first) && first.SendText == "AA", "Replugging keeps the same session and configuration");
    secondSerial.Fail(new IOException("test device fault")); Pump();
    Check(!second.IsConnected && shown.Last().Type == NotificationType.Error, "Device faults close the session and show a notification");

    var tab = vm.RightTabs.First(t => t.Id == "logs");
    vm.RightTabs.Remove(tab); vm.TopTabs.Add(tab); vm.TopSelected = tab;
    var portsTab = vm.LeftTabs.Single(); vm.LeftTabs.Remove(portsTab); vm.TopTabs.Add(portsTab);
    vm.LeftWidth = new GridLength(340); vm.ReceiveHeight = new GridLength(3, GridUnitType.Star);
    vm.PortsHeight = new GridLength(2, GridUnitType.Star);
    vm.RightWidth = new GridLength(360);
    vm.SettingsPanel.Style.ClassicHeader = true;
    logVm.SearchText = "TX";
    logVm.SetEnabledLevels(Serilog.Events.LogEventLevel.Warning, Serilog.Events.LogEventLevel.Error, Serilog.Events.LogEventLevel.Fatal);
    vm.IsCompact = true;
    vm.ToggleLanguageCommand.Execute(null); workspace.SelectedPort = first; second.IsReceivePaused = true;
    Pump(850); // Do not call a save command or Flush: verify automatic persistence itself.
    using var reloaded = new SettingsService(log);
    Check(reloaded.Current.Ports["COM1"].BaudRate == 9600 && reloaded.Current.Ports["COM2"].BaudRate == 57600, "Rx automatically saves independent port parameters");
    Check(reloaded.Current.Ports["COM1"].SendText == "AA" && reloaded.Current.Ports["COM2"].SendText == "second", "Rx automatically saves each payload");
    Check(reloaded.Current.Layout.TopTabs.Contains("logs") && reloaded.Current.Layout.TopTabs.Contains("ports") && reloaded.Current.Layout.TopSelected == "logs" && reloaded.Current.Layout.LeftWidth == "340", "Rx automatically saves the pre-compact layout");
    Check(reloaded.Current.Layout.Language == "en-US" && reloaded.Current.Layout.SelectedPort == "COM1", "Language and selection save automatically");
    Check(reloaded.Current.Layout.IsCompact && reloaded.Current.Layout.RightWidth == "360" && reloaded.Current.Layout.RightTabs!.Contains("ai"), "Compact mode and right dock automatically persist");
    Check(reloaded.Current.Ai.Model == "test-model" && reloaded.Current.Ai.Draft == "saved-draft"
        && reloaded.Current.Ai.ApiKey == "unit-test-secret" && File.ReadAllText(settings.FilePath).Contains("unit-test-secret"),
        "AI configuration, draft and plaintext API key save automatically");
    using (var keyRestored = new AiSettingsViewModel(reloaded, vm.Text))
        Check(keyRestored.ApiKey == "unit-test-secret", "Plaintext API key restores without platform-specific protection");
    Check(reloaded.Current.Appearance.ClassicHeader && reloaded.Current.LogFilter.SearchText == "TX"
        && reloaded.Current.LogFilter.EnabledLevels!.Contains("Warning") && reloaded.Current.LogFilter.MinimumLevel == "Warning",
        "Appearance and log filters automatically persist");
    using (var restoredStyle = new StyleViewModel(reloaded, vm.Text))
        Check(restoredStyle.TabPresentation == GOZA.Dock.DockTabPresentation.ClassicTabs && restoredStyle.PanePresentation == GOZA.Dock.DockPanePresentation.ClassicSeams, "Appearance restores from disk");
    Check(reloaded.Current.Ports["COM2"].IsReceivePaused && reloaded.Current.Serial is null, "Display preferences save and legacy configuration is removed after migration");
    var restored = new MainViewModel(workspace, reloaded, log, notifications,
        provider.GetRequiredService<ConnectionViewModel>(), provider.GetRequiredService<PortsViewModel>(), provider.GetRequiredService<ReceiveViewModel>(),
        provider.GetRequiredService<SendViewModel>(), provider.GetRequiredService<LogViewModel>(), provider.GetRequiredService<HelpViewModel>(), provider.GetRequiredService<SettingsViewModel>(), provider.GetRequiredService<AiViewModel>());
    Check(restored.IsCompact && restored.LeftTabs.Single().Id == "ports" && restored.TopTabs.Single().Id == "receive" && restored.LeftWidth.Value == 340 && restored.ReceiveHeight.Value == 3, "Compact restore filters to the four core regions");
    restored.IsCompact = false;
    Check(restored.TopTabs.Any(t => t.Id == "logs") && restored.TopTabs.Any(t => t.Id == "ports") && restored.LeftTabs.Count == 0 && restored.ConfigurationSelected?.Id == "connection" && restored.PortsHeight.Value == 2, "Leaving compact restores the saved full layout");
    var textLog = string.Join("\n", Directory.EnumerateFiles(log.LogDirectory).Select(path =>
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream); return reader.ReadToEnd();
    }));
    Check(textLog.Contains("TX COM1") && textLog.Contains("TX COM2") && textLog.Contains("RX") && textLog.Contains("InvalidPayload"), "Serilog persists both ports and errors");
    Check(log.ReadRecent("COM1").All(line => !line.Contains("[COM2]")), "Persisted file history stays per-port");
    Check(first.Logs.Count > 0, "Session log panel only retains the current run");
    first.ClearLogsCommand.Execute(null); Pump();
    Check(first.Logs.Count == 0 && Directory.EnumerateFiles(log.LogDirectory).Any() && log.ReadRecent("COM1").Any(), "Clear display only clears the session panel and leaves persisted files");
    File.WriteAllText(settings.FilePath, "{ invalid json");
    using var corrupt = new SettingsService(log);
    Check(corrupt.Error is not null && corrupt.Current.Ports.Count == 0, "Corrupt settings fall back safely");
    settings.Flush();
    window.Close(); vm.ShutdownCommand.Execute(null);
    Console.WriteLine($"Completed {checks} checks. Screenshots: {output}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    if (Application.Current is TestApp app) app.Mvvm.ServiceProvider?.GetService<MainViewModel>()?.ShutdownCommand.Execute(null);
    return 1;
}

public sealed class TestApp : App
{
    public static string DataPath { get; set; } = "";
    public override void RegisterServices(IServiceCollection services)
    {
        base.RegisterServices(services);
        services.RemoveAll<ISerialPortFactory>(); services.RemoveAll<AppLogService>();
        services.AddSingleton(new AppLogService(DataPath));
        services.RemoveAll<IAiChatClientFactory>();
        services.AddSingleton<FakeAiChatClientFactory>();
        services.AddSingleton<IAiChatClientFactory>(p => p.GetRequiredService<FakeAiChatClientFactory>());
        services.AddSingleton<FakeSerialFactory>();
        services.AddSingleton<ISerialPortFactory>(p => p.GetRequiredService<FakeSerialFactory>());
    }
}

public sealed class FakeSerialFactory : ISerialPortFactory
{
    public bool IsSupported => true;
    public int DiscoveryCount { get; private set; }
    public string[] Names { get; set; } = ["COM1", "COM2"];
    public List<FakeSerialPort> Created { get; } = [];
    public string[] GetPortNames() { DiscoveryCount++; return Names; }
    public ISerialPortService Create() { var port = new FakeSerialPort(); Created.Add(port); return port; }
}

public sealed class FakeSerialPort : ISerialPortService
{
    public bool IsSupported => true;
    public int Writes { get; private set; }
    public byte[]? LastWrite { get; private set; }
    public event Action<byte[]>? Received;
    public event Action<Exception>? Faulted;
    public string[] GetPortNames() => [];
    public Task OpenAsync(SerialSettings settings) => Task.CompletedTask;
    public Task CloseAsync() => Task.CompletedTask;
    public Task WriteAsync(byte[] bytes) { Writes++; LastWrite = bytes; return Task.CompletedTask; }
    public void Receive(byte[] bytes) => Received?.Invoke(bytes);
    public void Fail(Exception error) => Faulted?.Invoke(error);
    public void Dispose() { }
}

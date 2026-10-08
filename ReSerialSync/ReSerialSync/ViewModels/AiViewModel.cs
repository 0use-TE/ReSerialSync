using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotKernel;
using ReSerialSync.Extensions;
using ReSerialSync.Models;
using ReSerialSync.Services;

namespace ReSerialSync.ViewModels;

public sealed partial class AiViewModel : DockTabViewModel
{
    private readonly SerialAiService _service;
    private readonly NotificationService _notifications;
    private readonly CompositeDisposable _subscriptions = new();
    public ObservableCollection<AiChatEntry> Messages { get; } = [];
    public bool IsChatEmpty => Messages.Count == 0;
    public event Action? OpenSettingsRequested;
    [ObservableProperty] private string _prompt = "";
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(SendCommand)), NotifyCanExecuteChangedFor(nameof(ClearChatCommand))]
    private bool _isBusy;
    private CancellationTokenSource? _cancel;
    private bool _disposed;
    public AiViewModel(SerialWorkspaceViewModel workspace, SerialAiService service, SettingsService settings, NotificationService notifications)
        : base("ai", "AI", workspace)
    {
        _service = service; _notifications = notifications; Prompt = settings.Current.Ai.Draft;
        _subscriptions.Add(Messages.ObserveCollection().Subscribe(_ => OnPropertyChanged(nameof(IsChatEmpty))));
        _subscriptions.Add(this.ObserveProperties(nameof(Prompt)).Subscribe(_ => { settings.Current.Ai.Draft = Prompt; settings.ScheduleSave(); SendCommand.NotifyCanExecuteChanged(); }));
        _subscriptions.Add(Text.ObserveProperties(nameof(LocalizationService.Language)).Subscribe(_ => { foreach (var message in Messages) message.RefreshLanguage(); }));
    }
    private bool CanSend() => !IsBusy && !string.IsNullOrWhiteSpace(Prompt);
    private bool CanClear() => !IsBusy;
    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        if (_disposed || IsBusy) return;
        try { _service.ValidateConfiguration(); }
        catch (InvalidOperationException) { _notifications.Warning(Text["AI"], Text["AiConfigureFirst"]); OpenSettingsRequested?.Invoke(); return; }
        var prompt = Prompt.Trim();
        if (prompt.Length > 16000) { _notifications.Warning(Text["AI"], Text["AiPromptTooLong"]); return; }
        IsBusy = true; _cancel = new CancellationTokenSource();
        var user = new AiChatEntry("AiYou", Text); user.Markdown.Append(prompt); Messages.Add(user); Prompt = "";
        var reply = new AiChatEntry("AI", Text); Messages.Add(reply);
        var tools = new Dictionary<string, AiChatEntry>();
        try
        {
            await foreach (var update in _service.StreamAsync(prompt, _cancel.Token))
            {
                if (_disposed) break;
                switch (update.Kind)
                {
                    case KernelStreamingUpdateKind.TextDelta:
                        reply.Markdown.Append(update.TextDelta); break;
                    case KernelStreamingUpdateKind.ToolCallStarted:
                        var tool = new AiChatEntry("AiTool", Text); tool.Markdown.Append($"`{update.ToolName}`");
                        tools[update.ToolCallId ?? ""] = tool;
                        if (string.IsNullOrEmpty(reply.Markdown.ToString())) Messages.Remove(reply);
                        else reply = new AiChatEntry("AI", Text);
                        Messages.Add(tool); Messages.Add(reply); break;
                    case KernelStreamingUpdateKind.ToolCallCompleted:
                        if (tools.TryGetValue(update.ToolCallId ?? "", out var completed)) completed.Markdown.Append("\n\n```text\n" + update.ToolResult + "\n```");
                        break;
                    case KernelStreamingUpdateKind.Completed:
                        if (!string.IsNullOrEmpty(update.FinalText) && string.IsNullOrEmpty(reply.Markdown.ToString())) reply.Markdown.Append(update.FinalText);
                        break;
                }
            }
        }
        catch (OperationCanceledException) { if (!_disposed) reply.Markdown.Append("\n\n*" + Text["AiStopped"] + "*"); }
        catch (Exception)
        {
            if (!_disposed) { reply.Markdown.Append("\n\n" + Text["AiRequestFailed"]); _notifications.Error(Text["AI"], Text["AiRequestFailed"]); Prompt = prompt; }
        }
        finally { _cancel?.Dispose(); _cancel = null; IsBusy = false; }
    }
    [RelayCommand] private void Stop() => _cancel?.Cancel();
    [RelayCommand(CanExecute = nameof(CanClear))] private void ClearChat() { Messages.Clear(); _service.Clear(); }
    [RelayCommand] private void OpenSettings() => OpenSettingsRequested?.Invoke();
    [RelayCommand] private void UsePrompt(string prompt) => Prompt = prompt;
    [RelayCommand] private void ScrollChat(ScrollViewer? viewer) => viewer?.ScrollToEnd();
    public override void Dispose() { _disposed = true; _cancel?.Cancel(); _subscriptions.Dispose(); _service.Dispose(); base.Dispose(); }
}

using System;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ReSerialSync.Models;
using ReSerialSync.ViewModels;

namespace ReSerialSync.Views;

public partial class ReceiveView : UserControl
{
    private ReceiveViewModel? _model;
    private SerialSessionViewModel? _session;

    public ReceiveView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => AttachModel();
        ReceiveBox.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AttachModel();
    }

    private void AttachModel()
    {
        if (_model is not null) _model.PropertyChanged -= OnModelPropertyChanged;
        _model = DataContext as ReceiveViewModel;
        if (_model is not null) _model.PropertyChanged += OnModelPropertyChanged;
        AttachSession();
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReceiveViewModel.Session)) AttachSession();
    }

    private void AttachSession()
    {
        if (_session is not null) _session.ReceiveAppended -= OnReceiveAppended;
        _session = _model?.Session;
        if (_session is not null) _session.ReceiveAppended += OnReceiveAppended;
        Dispatcher.UIThread.Post(() => _session?.ScrollReceiveCommand.Execute(ReceiveBox), DispatcherPriority.Background);
    }

    private void OnReceiveAppended(object? sender, EventArgs e) => _session?.ScrollReceiveCommand.Execute(ReceiveBox);

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.Control || _session is null) return;
        if (e.Key == Key.A)
        {
            ReceiveBox.SelectAll();
            e.Handled = true;
            return;
        }
        if (e.Key != Key.C) return;
        var text = CopyText();
        if (text.Length == 0) return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return;
        await clipboard.SetTextAsync(text);
        e.Handled = true;
    }

    private string CopyText()
    {
        if (ReceiveBox.SelectedItems is not { Count: > 0 } selected) return "";
        if (selected.Count == ReceiveBox.ItemCount) return _session!.ReceiveText;
        var chosen = selected.Cast<ReceiveRow>().ToHashSet();
        var builder = new StringBuilder();
        foreach (var row in _session!.ReceiveLines)
        {
            if (!chosen.Contains(row)) continue;
            builder.Append(row.Text);
            builder.Append(row.Ending);
        }
        return builder.ToString();
    }
}

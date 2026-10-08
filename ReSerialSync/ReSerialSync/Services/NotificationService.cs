using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;

namespace ReSerialSync.Services;

public sealed class NotificationService
{
    private WindowNotificationManager? _manager;
    private readonly Queue<Notification> _pending = new();
    public event Action<Notification>? Shown;

    public void Attach(Control? anchor)
    {
        var host = anchor is null ? null : TopLevel.GetTopLevel(anchor);
        if (host is null || _manager is not null) return;
        _manager = new WindowNotificationManager(host) { Position = NotificationPosition.TopRight, MaxItems = 3 };
        while (_pending.TryDequeue(out var notification)) _manager.Show(notification);
    }

    public void Error(string title, string message) => Show(title, message, NotificationType.Error);
    public void Warning(string title, string message) => Show(title, message, NotificationType.Warning);
    public void Success(string title, string message) => Show(title, message, NotificationType.Success);

    private void Show(string title, string message, NotificationType type)
    {
        if (!Dispatcher.UIThread.CheckAccess()) { Dispatcher.UIThread.Post(() => Show(title, message, type)); return; }
        var notification = new Notification(title, message, type, TimeSpan.FromSeconds(type == NotificationType.Error ? 8 : 4));
        Shown?.Invoke(notification);
        if (_manager is not null) _manager.Show(notification);
        else
        {
            if (_pending.Count == 10) _pending.Dequeue();
            _pending.Enqueue(notification);
        }
    }
}

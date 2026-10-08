using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using Avalonia.Threading;

namespace ReSerialSync.Extensions;

public static class ObservableExtensions
{
    private static readonly IScheduler UiScheduler = new SynchronizationContextScheduler(new UiContext());

    public static IObservable<Unit> ObserveProperties(this INotifyPropertyChanged source, params string[] names) =>
        Observable.FromEvent<PropertyChangedEventHandler, PropertyChangedEventArgs>(
            handler => (_, args) => handler(args),
            handler => source.PropertyChanged += handler,
            handler => source.PropertyChanged -= handler)
        .Where(args => string.IsNullOrEmpty(args.PropertyName) || names.Length == 0 || names.Contains(args.PropertyName))
        .Select(_ => Unit.Default);

    public static IObservable<Unit> ObserveCollection(this INotifyCollectionChanged source) =>
        Observable.FromEvent<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
            handler => (_, args) => handler(args),
            handler => source.CollectionChanged += handler,
            handler => source.CollectionChanged -= handler).Select(_ => Unit.Default);

    public static IObservable<T> ObserveOnUi<T>(this IObservable<T> source) => source.ObserveOn(UiScheduler);

    public static IDisposable AutoSave(this IObservable<Unit> changes, Action save) =>
        changes.Throttle(TimeSpan.FromMilliseconds(500)).ObserveOnUi().Subscribe(_ => save());

    private sealed class UiContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state) =>
            Dispatcher.UIThread.Post(() => callback(state));
    }
}

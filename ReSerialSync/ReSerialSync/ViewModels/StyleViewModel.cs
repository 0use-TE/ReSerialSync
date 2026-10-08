using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using GOZA.Dock;
using ReSerialSync.Extensions;
using ReSerialSync.Services;

namespace ReSerialSync.ViewModels;

public partial class StyleViewModel : ViewModelBase, IDisposable
{
    private readonly CompositeDisposable _subscriptions = new();
    public LocalizationService Text { get; }
    [ObservableProperty] private bool _classicHeader;
    public DockTabPresentation TabPresentation => ClassicHeader ? DockTabPresentation.ClassicTabs : DockTabPresentation.ModernPills;
    public DockPanePresentation PanePresentation => ClassicHeader ? DockPanePresentation.ClassicSeams : DockPanePresentation.ModernCards;
    public StyleViewModel(SettingsService settings, LocalizationService text)
    {
        Text = text;
        ClassicHeader = settings.Current.Appearance.ClassicHeader;
        _subscriptions.Add(this.ObserveProperties(nameof(ClassicHeader)).Subscribe(_ =>
        {
            settings.Current.Appearance.ClassicHeader = ClassicHeader;
            OnPropertyChanged(nameof(TabPresentation)); OnPropertyChanged(nameof(PanePresentation));
            settings.ScheduleSave();
        }));
    }
    public void Dispose() => _subscriptions.Dispose();
}

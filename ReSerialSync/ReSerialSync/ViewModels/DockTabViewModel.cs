using GOZA.Dock;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReSerialSync.Extensions;
using ReSerialSync.Services;

namespace ReSerialSync.ViewModels;

public abstract class DockTabViewModel : ViewModelBase, IDockTabItem, IDisposable
{
    private readonly string _headerKey;
    private readonly CompositeDisposable _subscriptions = new();
    public string Id { get; }
    public string Header => Text[_headerKey];
    public bool ReuseSurface => true;
    public bool IsClosable => false;
    public SerialWorkspaceViewModel Workspace { get; }
    public SerialSessionViewModel? Session => Workspace.SelectedPort;
    public LocalizationService Text => Workspace.Text;

    protected DockTabViewModel(string id, string headerKey, SerialWorkspaceViewModel workspace)
    {
        Id = id; _headerKey = headerKey; Workspace = workspace;
        _subscriptions.Add(workspace.ObserveProperties(nameof(SerialWorkspaceViewModel.SelectedPort)).Subscribe(_ => OnPropertyChanged(nameof(Session))));
        _subscriptions.Add(Text.ObserveProperties(nameof(LocalizationService.Language)).Subscribe(_ => OnPropertyChanged(nameof(Header))));
    }
    public virtual void Dispose() => _subscriptions.Dispose();
}

using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Crystal.Avalonia;

internal static class EventToCommandChecks
{
    public static void Run(Action<bool, string> check)
    {
        var button = new Button();
        var command = new ProbeCommand();
        EventToCommand.SetCommand(button, command);
        EventToCommand.SetCommandParameter(button, "parameter");
        EventToCommand.SetEventName(button, "ClickEvent");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(command.Count == 1 && Equals(command.Parameter, "parameter"),
            "Crystal resolves Event-suffixed names through the routed event registry");

        EventToCommand.SetEventName(button, "Loaded");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(command.Count == 1, "Changing event names removes the old subscription");
        var loaded = new RoutedEventArgs(Control.LoadedEvent);
        EventToCommand.SetPassEventArgs(button, true);
        button.RaiseEvent(loaded);
        check(command.Count == 2 && ReferenceEquals(command.Parameter, loaded),
            "Crystal resolves inherited Loaded events and passes event arguments");
        command.Enabled = false;
        button.RaiseEvent(new RoutedEventArgs(Control.LoadedEvent));
        check(command.Count == 2, "Event binding respects CanExecute");
        command.Enabled = true;

        EventToCommand.SetRoutedEvent(button, Button.ClickEvent);
        button.RaiseEvent(new RoutedEventArgs(Control.LoadedEvent));
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(command.Count == 3, "Explicit routed events override EventName without duplicate handlers");
        EventToCommand.SetRoutedEvent(button, null);
        button.RaiseEvent(new RoutedEventArgs(Control.LoadedEvent));
        check(command.Count == 4, "Clearing an explicit event restores name-based binding");
        EventToCommand.SetEventName(button, null);
        button.RaiseEvent(new RoutedEventArgs(Control.LoadedEvent));
        check(command.Count == 4, "Clearing the event detaches its handler");

        var host = new DerivedProbeControl();
        var collectionCommand = new ProbeCommand();
        var mapping = new EventBinding { RoutedEvent = ProbeControl.PulseEvent,
            Command = collectionCommand, CommandParameter = "collection" };
        var bindings = EventToCommand.GetBindings(host);
        bindings.Add(mapping);
        host.RaiseEvent(new RoutedEventArgs(ProbeControl.PulseEvent));
        check(collectionCommand.Count == 1 && Equals(collectionCommand.Parameter, "collection"),
            "Collection bindings support custom explicit routed events under AOT");

        mapping.EventName = "Pulse";
        mapping.RoutedEvent = null;
        host.RaiseEvent(new RoutedEventArgs(ProbeControl.PulseEvent));
        check(collectionCommand.Count == 2, "Registered custom events resolve through inherited owners");
        mapping.CommandParameter = "updated";
        host.RaiseEvent(new RoutedEventArgs(ProbeControl.PulseEvent));
        check(collectionCommand.Count == 3 && Equals(collectionCommand.Parameter, "updated"),
            "Collection bindings read updated command parameters");
        bindings.Remove(mapping);
        host.RaiseEvent(new RoutedEventArgs(ProbeControl.PulseEvent));
        check(collectionCommand.Count == 3, "Removing a collection mapping detaches its handler");
        bindings.Add(mapping);
        EventToCommand.SetBindings(host, new EventBindingCollection());
        mapping.EventName = "Loaded";
        bindings.Clear();
        host.RaiseEvent(new RoutedEventArgs(ProbeControl.PulseEvent));
        host.RaiseEvent(new RoutedEventArgs(Control.LoadedEvent));
        check(collectionCommand.Count == 3, "Replacing a binding collection releases old mappings");

        if (RuntimeFeature.IsDynamicCodeSupported)
        {
            var clr = new ClrProbeControl();
            var clrCommand = new ProbeCommand();
            EventToCommand.SetEventName(clr, "Ping");
            EventToCommand.SetCommand(clr, clrCommand);
            clr.PingNow();
            check(clrCommand.Count == 1, "Legacy CLR EventHandler bindings remain compatible on JIT runtimes");
        }
    }

    private sealed class ProbeCommand : ICommand
    {
        public int Count { get; private set; }
        public object? Parameter { get; private set; }
        public bool Enabled { get; set; } = true;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => Enabled;
        public void Execute(object? parameter) { Count++; Parameter = parameter; }
    }

    private class ProbeControl : Control
    {
        public static readonly RoutedEvent<RoutedEventArgs> PulseEvent =
            RoutedEvent.Register<ProbeControl, RoutedEventArgs>("Pulse", RoutingStrategies.Direct);
        public ProbeControl() => _ = PulseEvent;
    }
    private sealed class DerivedProbeControl : ProbeControl;
    private sealed class ClrProbeControl : Control
    {
        public event EventHandler? Ping;
        public void PingNow() => Ping?.Invoke(this, EventArgs.Empty);
    }
}

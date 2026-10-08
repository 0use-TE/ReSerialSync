using Avalonia;
using Avalonia.Markup.Xaml;
using Crystal.Avalonia;
using Microsoft.Extensions.DependencyInjection;
using ReSerialSync.ViewModels;
using ReSerialSync.Views;
using System;
using Avalonia.Controls.ApplicationLifetimes;
using ReSerialSync.Services;

namespace ReSerialSync
{
    public partial class App : CrystalApplication
    {
        public override void Initialize() => AvaloniaXamlLoader.Load(this);

        public override void RegisterServices(IServiceCollection services)
        {
            services.AddSingleton<AppLogService>();
            services.AddSingleton<SettingsService>();
            services.AddSingleton<LocalizationService>();
            services.AddSingleton<NotificationService>();
            services.AddSingleton<ISerialPortFactory, SerialPortFactory>();
            services.AddSingleton<SerialWorkspaceViewModel>();
            services.AddSingleton<SerialAiPlugin>();
            services.AddSingleton<IAiChatClientFactory, AiChatClientFactory>();
            services.AddSingleton<SerialAiService>();
            services.AddMvvmSingleton<AiSettingsView, AiSettingsViewModel>();
            services.AddMvvmSingleton<AiView, AiViewModel>();
            services.AddMvvmSingleton<MainView, MainViewModel>();
            services.AddMvvmSingleton<ConnectionView, ConnectionViewModel>();
            services.AddMvvmSingleton<PortsView, PortsViewModel>();
            services.AddMvvmSingleton<ReceiveView, ReceiveViewModel>();
            services.AddMvvmSingleton<SendView, SendViewModel>();
            services.AddMvvmSingleton<LogView, LogViewModel>();
            services.AddMvvmSingleton<HelpView, HelpViewModel>();
            services.AddMvvmSingleton<StyleView, StyleViewModel>();
            services.AddMvvmSingleton<SettingsView, SettingsViewModel>();
        }

        public override void CreateShell(IServiceProvider serviceProvider)
        {
            CreateShell<MainWindow, MainView>();
            // Application lifetime plumbing; views contain no event handlers.
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Exit += (_, _) => serviceProvider.GetRequiredService<MainViewModel>().ShutdownCommand.Execute(null);
        }
    }
}

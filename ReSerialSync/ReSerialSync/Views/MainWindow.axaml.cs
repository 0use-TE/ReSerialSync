using Avalonia.Controls;

namespace ReSerialSync.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = MainContent.DataContext;
    }

    private void OnMainContentDataContextChanged(object? sender, System.EventArgs e) => DataContext = MainContent.DataContext;
}

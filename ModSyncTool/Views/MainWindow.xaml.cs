using System.Windows;
using ModSyncTool.ViewModels;

namespace ModSyncTool.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            Loaded -= OnLoaded;
            await viewModel.InitializeAsync();
        }
    }
}

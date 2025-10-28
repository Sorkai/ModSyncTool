using System.Windows;
using System.Windows.Controls;
using ModSyncTool.Models;
using ModSyncTool.ViewModels;

namespace ModSyncTool.Views;

public partial class LocalFilesWindow : Window
{
    private readonly LocalFilesViewModel _viewModel;

    public LocalFilesWindow(LocalFilesViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await _viewModel.InitializeAsync();
    }

    private void OnClosed(object? sender, System.EventArgs e)
    {
        Closed -= OnClosed;
    }

    private async void OnAddIgnoreClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.CommandParameter is LocalFileNode node)
        {
            if (node.Status != LocalFileStatus.Untracked)
            {
                return;
            }

            await _viewModel.AddIgnoreAsync(node);
        }
    }
}

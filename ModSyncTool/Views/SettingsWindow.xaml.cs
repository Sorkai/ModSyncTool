using System.IO;
using System.Windows;
using Microsoft.Win32;
using ModSyncTool.ViewModels;

namespace ModSyncTool.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Loaded += OnLoaded;
        Closed += OnClosed;
        _viewModel.RequestClose += OnRequestClose;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await _viewModel.InitializeAsync();
    }

    private void OnRequestClose(object? sender, System.EventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnClosed(object? sender, System.EventArgs e)
    {
        _viewModel.RequestClose -= OnRequestClose;
        Closed -= OnClosed;
        Loaded -= OnLoaded;
    }

    private void OnBrowseLaunchExecutable(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            var selected = dialog.FileName;
            var root = AppContext.BaseDirectory;
            try
            {
                var relative = Path.GetRelativePath(root, selected);
                if (!relative.StartsWith(".."))
                {
                    _viewModel.LaunchExecutable = relative.Replace('\\', '/');
                    return;
                }
            }
            catch
            {
                // fall back to absolute path
            }

            _viewModel.LaunchExecutable = selected;
        }
    }
}

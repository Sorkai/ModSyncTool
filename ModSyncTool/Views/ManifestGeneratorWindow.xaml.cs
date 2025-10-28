using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Forms = System.Windows.Forms;
using ModSyncTool.Models;
using ModSyncTool.ViewModels;

namespace ModSyncTool.Views;

public partial class ManifestGeneratorWindow : Window
{
    private readonly ManifestGeneratorViewModel _viewModel;

    public ManifestGeneratorWindow(ManifestGeneratorViewModel viewModel)
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

    private async void OnSelectFolderClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "选择包含所有 Mod 文件的根目录"
        };

        var helper = new WindowInteropHelper(this);
        var result = dialog.ShowDialog(new Win32Window(helper.Handle));
        if (result == Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            await _viewModel.ScanFolderAsync(dialog.SelectedPath);
        }
    }

    private void OnTreeViewSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is PublishFileItem item)
        {
            _viewModel.SetFocusedItem(item);
        }
        else
        {
            _viewModel.SetFocusedItem(null);
        }
    }

    private async void OnGenerateManifestClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var (manifest, profile) = _viewModel.BuildManifest();
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                FileName = "online_manifest.json"
            };

            if (saveDialog.ShowDialog(this) != true)
            {
                return;
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(manifest, options);
            await File.WriteAllTextAsync(saveDialog.FileName, json);

            await _viewModel.PersistProfileAsync(profile);
            System.Windows.MessageBox.Show(this, "Manifest 已生成，请上传到服务器。", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (System.Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private sealed class Win32Window : Forms.IWin32Window
    {
        public Win32Window(nint handle)
        {
            Handle = handle;
        }

        public nint Handle { get; }
    }
}

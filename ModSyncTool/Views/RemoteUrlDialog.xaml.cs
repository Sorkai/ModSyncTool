using System.Windows;
using ModSyncTool.ViewModels;

namespace ModSyncTool.Views;

public partial class RemoteUrlDialog : Window
{
    private readonly RemoteUrlDialogViewModel _viewModel = new();

    public RemoteUrlDialog()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    public string EnteredUrl => _viewModel.Url;

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_viewModel.Url))
        {
            System.Windows.MessageBox.Show(this, "请输入有效的链接", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}

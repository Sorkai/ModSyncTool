using System.Windows;
using ModSyncTool.Models;

namespace ModSyncTool.Views;

public partial class LaunchMismatchDialog : Window
{
    public LaunchMismatchDialog(string remoteLaunchExecutable, string remoteVersion)
    {
        InitializeComponent();
        Message = $"远程版本 {remoteVersion} 推荐启动程序: {remoteLaunchExecutable}.";
        DataContext = this;
    }

    public string Message { get; }

    public LaunchMismatchDecision Result { get; private set; } = LaunchMismatchDecision.Cancel;

    private void OnIgnoreOnceClick(object sender, RoutedEventArgs e)
    {
        Result = LaunchMismatchDecision.IgnoreOnce;
        DialogResult = true;
    }

    private void OnIgnoreForeverClick(object sender, RoutedEventArgs e)
    {
        Result = LaunchMismatchDecision.IgnorePermanently;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Result = LaunchMismatchDecision.Cancel;
        DialogResult = false;
    }
}

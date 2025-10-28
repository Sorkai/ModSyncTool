using System;
using ModSyncTool.Models;
using ModSyncTool.Views;

namespace ModSyncTool.Services;

public sealed class DialogService : IDialogService
{
    public WelcomeDialogChoice ShowWelcomeDialog()
    {
        return Invoke(() =>
        {
            var dialog = new WelcomeDialog
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            var result = dialog.ShowDialog();
            return result == true ? dialog.Result : WelcomeDialogChoice.Cancel;
        });
    }

    public string? PromptForRemoteUrl()
    {
        return Invoke(() =>
        {
            var dialog = new RemoteUrlDialog
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            var result = dialog.ShowDialog();
            return result == true ? dialog.EnteredUrl : null;
        });
    }

    public void ShowInfo(string title, string message)
    {
        Invoke(() => System.Windows.MessageBox.Show(System.Windows.Application.Current.MainWindow!, message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information));
    }

    public void ShowWarning(string title, string message)
    {
        Invoke(() => System.Windows.MessageBox.Show(System.Windows.Application.Current.MainWindow!, message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning));
    }

    public void ShowError(string title, string message)
    {
        Invoke(() => System.Windows.MessageBox.Show(System.Windows.Application.Current.MainWindow!, message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error));
    }

    public LaunchMismatchDecision ShowLaunchMismatchDialog(string remoteLaunchExecutable, string remoteVersion)
    {
        return Invoke(() =>
        {
            var dialog = new LaunchMismatchDialog(remoteLaunchExecutable, remoteVersion)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            var result = dialog.ShowDialog();
            return result == true ? dialog.Result : LaunchMismatchDecision.Cancel;
        });
    }

    private static T Invoke<T>(Func<T> action)
    {
        if (System.Windows.Application.Current?.Dispatcher?.CheckAccess() == true)
        {
            return action();
        }

        return System.Windows.Application.Current!.Dispatcher.Invoke(action);
    }

    private static void Invoke(Action action)
    {
        if (System.Windows.Application.Current?.Dispatcher?.CheckAccess() == true)
        {
            action();
            return;
        }

        System.Windows.Application.Current!.Dispatcher.Invoke(action);
    }
}

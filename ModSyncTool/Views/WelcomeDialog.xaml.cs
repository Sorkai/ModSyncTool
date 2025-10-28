using System.Windows;
using ModSyncTool.Models;

namespace ModSyncTool.Views;

public partial class WelcomeDialog : Window
{
    public WelcomeDialog()
    {
        InitializeComponent();
    }

    public WelcomeDialogChoice Result { get; private set; } = WelcomeDialogChoice.Cancel;

    private void OnInputLinkClick(object sender, RoutedEventArgs e)
    {
        Result = WelcomeDialogChoice.InputRemoteUrl;
        DialogResult = true;
    }

    private void OnSkipOnceClick(object sender, RoutedEventArgs e)
    {
        Result = WelcomeDialogChoice.SkipOnce;
        DialogResult = true;
    }

    private void OnSkipAlwaysClick(object sender, RoutedEventArgs e)
    {
        Result = WelcomeDialogChoice.SkipPermanently;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Result = WelcomeDialogChoice.Cancel;
        DialogResult = false;
    }
}

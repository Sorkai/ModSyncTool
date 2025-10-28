using ModSyncTool.Models;

namespace ModSyncTool.Services;

public interface IDialogService
{
    WelcomeDialogChoice ShowWelcomeDialog();

    string? PromptForRemoteUrl();

    void ShowInfo(string title, string message);

    void ShowWarning(string title, string message);

    void ShowError(string title, string message);

    LaunchMismatchDecision ShowLaunchMismatchDialog(string remoteLaunchExecutable, string remoteVersion);
}

namespace ModSyncTool.Models;

public sealed class SyncExecutionResult
{
    public bool Success { get; init; }

    public bool AppLaunched { get; init; }

    public string? ErrorMessage { get; init; }

    public OnlineManifest? RemoteManifest { get; init; }

    public LocalConfig? UpdatedLocalConfig { get; init; }

    public bool RequiresLaunchDecision { get; init; }

    public string? RemoteLaunchExecutable { get; init; }

    public string? RemoteVersion { get; init; }
}

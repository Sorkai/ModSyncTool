using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ModSyncTool.Models;

public sealed class LocalConfig
{
    [JsonPropertyName("current_version")]
    public string CurrentVersion { get; set; } = "0.0.0";

    [JsonPropertyName("update_url")]
    public string? UpdateUrl { get; set; }

    [JsonPropertyName("launch_executable")]
    public string LaunchExecutable { get; set; } = string.Empty;

    [JsonPropertyName("download_control")]
    public DownloadControlConfig? DownloadControl { get; set; }

    [JsonPropertyName("ignore_ssl_errors")]
    public bool IgnoreSslErrors { get; set; }

    [JsonPropertyName("ignored_remote_launch_version")]
    public string IgnoredRemoteLaunchVersion { get; set; } = string.Empty;

    [JsonPropertyName("ignore_patterns")]
    public List<string> IgnorePatterns { get; set; } = new();

    [JsonPropertyName("managed_files")]
    public List<string> ManagedFiles { get; set; } = new();
}

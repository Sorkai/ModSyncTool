using System.Text.Json.Serialization;

namespace ModSyncTool.Models;

public sealed class PublisherProfile
{
    [JsonPropertyName("last_generated_version")]
    public string LastGeneratedVersion { get; set; } = "0.0.0";

    [JsonPropertyName("info")]
    public string Info { get; set; } = string.Empty;

    [JsonPropertyName("base_download_url")]
    public string BaseDownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("append_version_to_path")]
    public bool AppendVersionToPath { get; set; }

    [JsonPropertyName("launch_executable")]
    public string LaunchExecutable { get; set; } = string.Empty;

    [JsonPropertyName("download_control_settings")]
    public DownloadControlConfig? DownloadControlSettings { get; set; }
}

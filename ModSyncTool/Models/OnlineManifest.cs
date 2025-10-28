using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ModSyncTool.Models;

public sealed class OnlineManifest
{
    [JsonPropertyName("info")]
    public string Info { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = "0.0.0";

    [JsonPropertyName("base_download_url")]
    public string BaseDownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("append_version_to_path")]
    public bool AppendVersionToPath { get; set; }

    [JsonPropertyName("launch_executable")]
    public string LaunchExecutable { get; set; } = string.Empty;

    [JsonPropertyName("download_control")]
    public DownloadControlConfig? DownloadControl { get; set; }

    [JsonPropertyName("files")]
    public List<ManifestFileEntry> Files { get; set; } = new();
}

using System.Text.Json.Serialization;

namespace ModSyncTool.Models;

public sealed class ManifestFileEntry
{
    [JsonPropertyName("relative_path")]
    public string RelativePath { get; set; } = string.Empty;

    [JsonPropertyName("hash")]
    public string Hash { get; set; } = string.Empty;

    [JsonPropertyName("download_segment")]
    public string DownloadSegment { get; set; } = string.Empty;

    [JsonPropertyName("override_base_url")]
    public string? OverrideBaseUrl { get; set; }
}

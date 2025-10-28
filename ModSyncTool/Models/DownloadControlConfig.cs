using System.Text.Json.Serialization;

namespace ModSyncTool.Models;

public sealed class DownloadControlConfig
{
    [JsonPropertyName("enable_multi_file_download")]
    public bool EnableMultiFileDownload { get; set; } = true;

    [JsonPropertyName("max_concurrent_files")]
    public int MaxConcurrentFiles { get; set; } = 10;

    [JsonPropertyName("enable_multi_thread_download")]
    public bool EnableMultiThreadDownload { get; set; }

    [JsonPropertyName("threads_per_file")]
    public int ThreadsPerFile { get; set; } = 8;

    public DownloadControlConfig Clone()
    {
        return new DownloadControlConfig
        {
            EnableMultiFileDownload = EnableMultiFileDownload,
            MaxConcurrentFiles = MaxConcurrentFiles,
            EnableMultiThreadDownload = EnableMultiThreadDownload,
            ThreadsPerFile = ThreadsPerFile
        };
    }
}

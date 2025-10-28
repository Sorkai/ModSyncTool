namespace ModSyncTool.Models;

public sealed class ResolvedDownloadStrategy
{
    public bool EnableMultiFileDownload { get; set; }

    public int MaxConcurrentFiles { get; set; }

    public bool EnableMultiThreadDownload { get; set; }

    public int ThreadsPerFile { get; set; }
}

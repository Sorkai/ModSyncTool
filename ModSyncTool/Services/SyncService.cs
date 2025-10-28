using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ModSyncTool.Models;
using Serilog;
using Microsoft.Win32.SafeHandles;

namespace ModSyncTool.Services;

public sealed class SyncService
{
    private static readonly DownloadControlConfig DefaultDownloadStrategy = new()
    {
        EnableMultiFileDownload = true,
        MaxConcurrentFiles = 10,
        EnableMultiThreadDownload = false,
        ThreadsPerFile = 4
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ConfigService _configService;
    private readonly HashService _hashService;
    private readonly FileScannerService _fileScannerService;
    private readonly string _rootDirectory;

    public SyncService(ConfigService configService, HashService hashService, FileScannerService fileScannerService)
    {
        _configService = configService;
        _hashService = hashService;
        _fileScannerService = fileScannerService;
        _rootDirectory = AppContext.BaseDirectory;
    }

    public async Task<LocalConfig?> InitializeFromRemoteAsync(string url, CancellationToken cancellationToken = default)
    {
        Log.Information("首次配置，开始从 URL 初始化");
        try
        {
            var manifest = await DownloadManifestAsync(url, ignoreSslErrors: false, cancellationToken).ConfigureAwait(false);
            var config = new LocalConfig
            {
                UpdateUrl = url,
                CurrentVersion = "0.0.0",
                LaunchExecutable = manifest.LaunchExecutable,
                DownloadControl = null,
                IgnoreSslErrors = false,
                IgnoredRemoteLaunchVersion = string.Empty,
                IgnorePatterns = new List<string>(),
                ManagedFiles = new List<string>()
            };

            await _configService.SaveLocalConfigAsync(config, cancellationToken).ConfigureAwait(false);
            Log.Warning("检查 local.launch_executable 路径是否存在 (首次大概率不存在)");
            return config;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "初始化远程配置失败");
            throw;
        }
    }

    public async Task<SyncExecutionResult> RunUpdateAndLaunchAsync(IProgress<string> statusProgress, IProgress<double?>? progressReporter, CancellationToken cancellationToken = default)
    {
        statusProgress.Report("正在检查更新...");
        Log.Information("正在检查更新...");

        var localConfig = await _configService.LoadLocalConfigAsync(cancellationToken).ConfigureAwait(false);
        if (localConfig == null)
        {
            const string message = "local_config.json 不存在";
            Log.Error(message);
            return new SyncExecutionResult { Success = false, ErrorMessage = message };
        }

        if (string.IsNullOrWhiteSpace(localConfig.UpdateUrl))
        {
            const string message = "update_url 未配置";
            Log.Error(message);
            return new SyncExecutionResult { Success = false, ErrorMessage = message, UpdatedLocalConfig = localConfig };
        }

        try
        {
            var httpClient = CreateHttpClient(localConfig.IgnoreSslErrors);
            if (localConfig.IgnoreSslErrors)
            {
                Log.Warning("SSL 证书验证已被用户忽略");
            }

            OnlineManifest remoteManifest;
            try
            {
                remoteManifest = await DownloadManifestAsync(httpClient, localConfig.UpdateUrl, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "下载远程清单失败");
                return new SyncExecutionResult { Success = false, ErrorMessage = ex.Message, UpdatedLocalConfig = localConfig };
            }

            Version localVersion;
            Version remoteVersion;
            try
            {
                localVersion = new Version(localConfig.CurrentVersion);
            }
            catch (Exception)
            {
                localVersion = new Version(0, 0, 0);
            }

            try
            {
                remoteVersion = new Version(remoteManifest.Version);
            }
            catch (Exception)
            {
                remoteVersion = localVersion;
            }

            if (localVersion >= remoteVersion)
            {
                Log.Information("已是最新版本");
                statusProgress.Report("已是最新");
            }
            else
            {
                Log.Information("发现新版本 {RemoteVersion}，开始更新", remoteManifest.Version);
                statusProgress.Report($"正在更新 v{remoteManifest.Version}...");
                await PerformUpdateAsync(localConfig, remoteManifest, httpClient, statusProgress, progressReporter, cancellationToken).ConfigureAwait(false);
                localConfig.CurrentVersion = remoteManifest.Version;
            }

            var requiresLaunchDecision = await EnsureLaunchExecutableAsync(localConfig, remoteManifest, statusProgress);

            localConfig.ManagedFiles = remoteManifest.Files.Select(f => f.RelativePath.Replace('\\', '/')).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            await _configService.SaveLocalConfigAsync(localConfig, cancellationToken).ConfigureAwait(false);

            var untracked = await _fileScannerService.FindUntrackedFilesAsync(localConfig, cancellationToken).ConfigureAwait(false);
            if (untracked.Count > 0)
            {
                foreach (var path in untracked)
                {
                    Log.Warning("发现未跟踪文件: {Path}", path);
                }

                statusProgress.Report("警告：发现未跟踪文件！");
            }
            else
            {
                statusProgress.Report("更新完成");
            }

            if (requiresLaunchDecision)
            {
                return new SyncExecutionResult
                {
                    Success = true,
                    AppLaunched = false,
                    UpdatedLocalConfig = localConfig,
                    RemoteManifest = remoteManifest,
                    RequiresLaunchDecision = true,
                    RemoteLaunchExecutable = remoteManifest.LaunchExecutable,
                    RemoteVersion = remoteManifest.Version
                };
            }

            var launched = await LaunchApplicationAsync(localConfig).ConfigureAwait(false);

            return new SyncExecutionResult
            {
                Success = true,
                AppLaunched = launched,
                UpdatedLocalConfig = localConfig,
                RemoteManifest = remoteManifest
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "更新流程发生异常");
            return new SyncExecutionResult { Success = false, ErrorMessage = ex.Message, UpdatedLocalConfig = localConfig };
        }
    }

    private async Task PerformUpdateAsync(LocalConfig localConfig, OnlineManifest remoteManifest, HttpClient httpClient, IProgress<string> statusProgress, IProgress<double?>? progressReporter, CancellationToken cancellationToken)
    {
        await CleanupObsoleteFilesAsync(localConfig, remoteManifest, cancellationToken).ConfigureAwait(false);

        var strategy = ResolveStrategy(localConfig.DownloadControl, remoteManifest.DownloadControl);
        Log.Debug("最终采纳的下载策略: {@Strategy}", strategy);

        if (remoteManifest.Files.Count == 0)
        {
            progressReporter?.Report(1.0);
            return;
        }

        var total = remoteManifest.Files.Count;
        var processed = 0;
        var parallelism = strategy.EnableMultiFileDownload ? Math.Max(1, strategy.MaxConcurrentFiles) : 1;
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = parallelism,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(remoteManifest.Files, parallelOptions, async (file, token) =>
        {
            var relative = file.RelativePath.Replace('\\', '/');
            var targetPath = Path.Combine(_rootDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            if (File.Exists(targetPath))
            {
                try
                {
                    var localHash = await _hashService.ComputeFileHashAsync(targetPath, token).ConfigureAwait(false);
                    if (string.Equals(localHash, file.Hash, StringComparison.OrdinalIgnoreCase))
                    {
                        Log.Debug("跳过 {File} (哈希一致)", relative);
                        IncrementProgress(progressReporter, total, ref processed);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "计算本地文件哈希失败: {File}", relative);
                }
            }

            statusProgress.Report($"正在下载 {relative}...");
            var tempPath = targetPath + ".tmp";
            try
            {
                await DownloadFileAsync(httpClient, file, remoteManifest, tempPath, strategy, token).ConfigureAwait(false);
                var downloadedHash = await _hashService.ComputeFileHashAsync(tempPath, token).ConfigureAwait(false);
                if (!string.Equals(downloadedHash, file.Hash, StringComparison.OrdinalIgnoreCase))
                {
                    Log.Error("文件 {File} 下载后哈希校验失败！", relative);
                    File.Delete(tempPath);
                    throw new InvalidOperationException($"文件 {relative} 下载后哈希校验失败！");
                }

                File.Move(tempPath, targetPath, overwrite: true);
                Log.Information("更新文件: {File}", relative);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "下载文件 {File} 失败", relative);
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                throw;
            }
            finally
            {
                IncrementProgress(progressReporter, total, ref processed);
            }
        }).ConfigureAwait(false);

        progressReporter?.Report(1.0);
    }

    private static void IncrementProgress(IProgress<double?>? progressReporter, int total, ref int processed)
    {
        if (progressReporter == null)
        {
            return;
        }

        var current = Interlocked.Increment(ref processed);
        progressReporter.Report(Math.Clamp((double)current / total, 0.0, 1.0));
    }

    private Task CleanupObsoleteFilesAsync(LocalConfig localConfig, OnlineManifest remoteManifest, CancellationToken cancellationToken)
    {
        var remotePaths = new HashSet<string>(remoteManifest.Files.Select(f => f.RelativePath.Replace('\\', '/')), StringComparer.OrdinalIgnoreCase);
        foreach (var managed in localConfig.ManagedFiles.ToList())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (remotePaths.Contains(managed))
            {
                continue;
            }

            var fullPath = Path.Combine(_rootDirectory, managed.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                Log.Information("清理陈旧文件: {File}", managed);
            }
        }

        return Task.CompletedTask;
    }

    private ResolvedDownloadStrategy ResolveStrategy(DownloadControlConfig? local, DownloadControlConfig? remote)
    {
        var remoteStrategy = remote ?? DefaultDownloadStrategy;
        if (local == null)
        {
            return new ResolvedDownloadStrategy
            {
                EnableMultiFileDownload = remoteStrategy.EnableMultiFileDownload,
                MaxConcurrentFiles = Math.Max(1, remoteStrategy.MaxConcurrentFiles),
                EnableMultiThreadDownload = remoteStrategy.EnableMultiThreadDownload,
                ThreadsPerFile = Math.Max(1, remoteStrategy.ThreadsPerFile)
            };
        }

        return new ResolvedDownloadStrategy
        {
            EnableMultiFileDownload = local.EnableMultiFileDownload && remoteStrategy.EnableMultiFileDownload,
            MaxConcurrentFiles = Math.Max(1, Math.Min(local.MaxConcurrentFiles, remoteStrategy.MaxConcurrentFiles)),
            EnableMultiThreadDownload = local.EnableMultiThreadDownload && remoteStrategy.EnableMultiThreadDownload,
            ThreadsPerFile = Math.Max(1, Math.Min(local.ThreadsPerFile, remoteStrategy.ThreadsPerFile))
        };
    }

    private async Task DownloadFileAsync(HttpClient httpClient, ManifestFileEntry file, OnlineManifest manifest, string tempPath, ResolvedDownloadStrategy strategy, CancellationToken cancellationToken)
    {
        var downloadUrl = BuildDownloadUrl(file, manifest);
        if (strategy.EnableMultiThreadDownload && strategy.ThreadsPerFile > 1)
        {
            var supportsRange = await SupportsRangeRequestsAsync(httpClient, downloadUrl, cancellationToken).ConfigureAwait(false);
            if (supportsRange.isSupported && supportsRange.contentLength > 0)
            {
                await DownloadWithRangesAsync(httpClient, downloadUrl, tempPath, supportsRange.contentLength, strategy.ThreadsPerFile, cancellationToken).ConfigureAwait(false);
                return;
            }

            Log.Warning("多线程下载不可用，回退到单线程: {Url}", downloadUrl);
        }

        await DownloadSingleThreadAsync(httpClient, downloadUrl, tempPath, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildDownloadUrl(ManifestFileEntry file, OnlineManifest manifest)
    {
        var baseUrl = string.IsNullOrWhiteSpace(file.OverrideBaseUrl) ? manifest.BaseDownloadUrl : file.OverrideBaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("在线清单缺少 base_download_url");
        }

        var trimmedBase = baseUrl.TrimEnd('/') + "/";
        if (manifest.AppendVersionToPath)
        {
            trimmedBase += manifest.Version.Trim('/') + "/";
        }

        return trimmedBase + file.DownloadSegment.TrimStart('/');
    }

    private static async Task DownloadSingleThreadAsync(HttpClient httpClient, string url, string tempPath, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var httpStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true);
        await httpStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
    }

    private async Task DownloadWithRangesAsync(HttpClient httpClient, string url, string tempPath, long contentLength, int threads, CancellationToken cancellationToken)
    {
        using var handle = File.OpenHandle(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, FileOptions.Asynchronous);
        RandomAccess.SetLength(handle, contentLength);

        var ranges = CalculateRanges(contentLength, threads);
        var tasks = ranges.Select(range => DownloadRangeAsync(httpClient, url, handle, range.start, range.end, cancellationToken));
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task DownloadRangeAsync(HttpClient httpClient, string url, SafeFileHandle handle, long start, long end, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Range = new RangeHeaderValue(start, end);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        var buffer = new byte[8192];
        var position = start;
        int read;
        while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            await RandomAccess.WriteAsync(handle, buffer.AsMemory(0, read), position, cancellationToken).ConfigureAwait(false);
            position += read;
        }
    }

    private static IEnumerable<(long start, long end)> CalculateRanges(long length, int threads)
    {
        var segmentSize = Math.Max(1, length / threads);
        var remainder = length % threads;
        long position = 0;
        for (var i = 0; i < threads; i++)
        {
            var currentSize = segmentSize + (i == threads - 1 ? remainder : 0);
            var start = position;
            var end = Math.Min(length - 1, start + currentSize - 1);
            yield return (start, end);
            position += currentSize;
            if (position >= length)
            {
                break;
            }
        }
    }

    private async Task<(bool isSupported, long contentLength)> SupportsRangeRequestsAsync(HttpClient httpClient, string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, url);
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return (false, 0);
            }

            if (!response.Headers.TryGetValues("Accept-Ranges", out var values) || !values.Contains("bytes", StringComparer.OrdinalIgnoreCase))
            {
                return (false, 0);
            }

            var length = response.Content.Headers.ContentLength ?? 0;
            return (length > 0, length);
        }
        catch
        {
            return (false, 0);
        }
    }

    private Task<bool> EnsureLaunchExecutableAsync(LocalConfig localConfig, OnlineManifest manifest, IProgress<string> statusProgress)
    {
        if (string.Equals(manifest.LaunchExecutable, localConfig.LaunchExecutable, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(false);
        }

        if (string.Equals(manifest.Version, localConfig.IgnoredRemoteLaunchVersion, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(false);
        }

        var remotePath = Path.Combine(_rootDirectory, manifest.LaunchExecutable.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(remotePath))
        {
            Log.Information("检测到新启动项，自动更新本地配置");
            localConfig.LaunchExecutable = manifest.LaunchExecutable;
            statusProgress.Report($"启动程序已自动更新为: {manifest.LaunchExecutable}");
            return Task.FromResult(false);
        }

        Log.Warning("远程推荐的新启动项 {LaunchExecutable} 在本地未找到", manifest.LaunchExecutable);
        return Task.FromResult(true);
    }

    public Task<bool> LaunchApplicationAsync(LocalConfig localConfig)
    {
        var launchPath = Path.Combine(_rootDirectory, localConfig.LaunchExecutable.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(launchPath))
        {
            Log.Error("找不到启动目标 {LaunchExecutable}", localConfig.LaunchExecutable);
            return Task.FromResult(false);
        }

        try
        {
            Log.Information("启动程序: {LaunchExecutable}", localConfig.LaunchExecutable);
            var startInfo = new ProcessStartInfo
            {
                FileName = launchPath,
                WorkingDirectory = Path.GetDirectoryName(launchPath) ?? _rootDirectory,
                UseShellExecute = true
            };
            Process.Start(startInfo);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "启动程序失败");
            return Task.FromResult(false);
        }
    }

    private static HttpClient CreateHttpClient(bool ignoreSslErrors)
    {
        var handler = new HttpClientHandler();
        if (ignoreSslErrors)
        {
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        }

        return new HttpClient(handler, disposeHandler: true)
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
    }

    private async Task<OnlineManifest> DownloadManifestAsync(string url, bool ignoreSslErrors, CancellationToken cancellationToken)
    {
        using var client = CreateHttpClient(ignoreSslErrors);
        return await DownloadManifestAsync(client, url, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<OnlineManifest> DownloadManifestAsync(HttpClient client, string url, CancellationToken cancellationToken)
    {
        Log.Debug("尝试从 {Url} 下载远程清单", url);
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var manifest = await JsonSerializer.DeserializeAsync<OnlineManifest>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        if (manifest == null)
        {
            throw new InvalidDataException("远程清单反序列化失败");
        }

        return manifest;
    }
}

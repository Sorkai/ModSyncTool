using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ModSyncTool.Commands;
using ModSyncTool.Helpers;
using ModSyncTool.Models;
using ModSyncTool.Services;
using Serilog;

namespace ModSyncTool.ViewModels;

public sealed class ManifestGeneratorViewModel : ObservableObject
{
    private readonly ConfigService _configService;
    private readonly HashService _hashService;

    private PublisherProfile? _profile;
    private string? _currentFolder;
    private bool _isBusy;
    private string _statusMessage = string.Empty;
    private string _info = string.Empty;
    private string _version = "1.0.0";
    private string _baseDownloadUrl = string.Empty;
    private bool _appendVersionToPath = true;
    private string _launchExecutable = string.Empty;
    private bool _enableMultiFileDownload = true;
    private int _maxConcurrentFiles = 10;
    private bool _enableMultiThreadDownload;
    private int _threadsPerFile = 8;
    private PublishFileItem? _focusedItem;
    private string _editDownloadSegment = string.Empty;
    private string? _editOverrideBaseUrl;

    public ManifestGeneratorViewModel(ConfigService configService, HashService hashService)
    {
        _configService = configService;
        _hashService = hashService;
        RootItems = new ObservableCollection<PublishFileItem>();
        RefreshProfileCommand = new AsyncRelayCommand(LoadProfileAsync, () => !IsBusy);
        ApplyToCurrentCommand = new RelayCommand(ApplyToCurrent, () => FocusedItem is { IsDirectory: false });
        ApplyToSelectedCommand = new RelayCommand(ApplyToSelectedFiles, () => RootItems.Count > 0);
    }

    public ObservableCollection<PublishFileItem> RootItems { get; }

    public AsyncRelayCommand RefreshProfileCommand { get; }

    public RelayCommand ApplyToCurrentCommand { get; }

    public RelayCommand ApplyToSelectedCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value)
            {
                return;
            }

            _isBusy = value;
            OnPropertyChanged();
            RefreshProfileCommand.RaiseCanExecuteChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string Info
    {
        get => _info;
        set => SetProperty(ref _info, value);
    }

    public string Version
    {
        get => _version;
        set => SetProperty(ref _version, value);
    }

    public string BaseDownloadUrl
    {
        get => _baseDownloadUrl;
        set => SetProperty(ref _baseDownloadUrl, value);
    }

    public bool AppendVersionToPath
    {
        get => _appendVersionToPath;
        set => SetProperty(ref _appendVersionToPath, value);
    }

    public string LaunchExecutable
    {
        get => _launchExecutable;
        set => SetProperty(ref _launchExecutable, value);
    }

    public bool EnableMultiFileDownload
    {
        get => _enableMultiFileDownload;
        set => SetProperty(ref _enableMultiFileDownload, value);
    }

    public int MaxConcurrentFiles
    {
        get => _maxConcurrentFiles;
        set => SetProperty(ref _maxConcurrentFiles, value);
    }

    public bool EnableMultiThreadDownload
    {
        get => _enableMultiThreadDownload;
        set => SetProperty(ref _enableMultiThreadDownload, value);
    }

    public int ThreadsPerFile
    {
        get => _threadsPerFile;
        set => SetProperty(ref _threadsPerFile, value);
    }

    public PublishFileItem? FocusedItem
    {
        get => _focusedItem;
        private set
        {
            if (_focusedItem == value)
            {
                return;
            }

            _focusedItem = value;
            OnPropertyChanged();
            ApplyToCurrentCommand.RaiseCanExecuteChanged();
        }
    }

    public string EditDownloadSegment
    {
        get => _editDownloadSegment;
        set => SetProperty(ref _editDownloadSegment, value);
    }

    public string? EditOverrideBaseUrl
    {
        get => _editOverrideBaseUrl;
        set => SetProperty(ref _editOverrideBaseUrl, value);
    }

    public async Task InitializeAsync()
    {
        await LoadProfileAsync();
    }

    public async Task LoadProfileAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "正在载入发布者配置...";
        Log.Information("发布工具启动，尝试加载 publisher_profile.json");
        try
        {
            _profile = await _configService.LoadPublisherProfileAsync();
            if (_profile != null)
            {
                Info = _profile.Info;
                BaseDownloadUrl = _profile.BaseDownloadUrl;
                AppendVersionToPath = _profile.AppendVersionToPath;
                LaunchExecutable = _profile.LaunchExecutable;
                if (_profile.DownloadControlSettings != null)
                {
                    EnableMultiFileDownload = _profile.DownloadControlSettings.EnableMultiFileDownload;
                    MaxConcurrentFiles = _profile.DownloadControlSettings.MaxConcurrentFiles;
                    EnableMultiThreadDownload = _profile.DownloadControlSettings.EnableMultiThreadDownload;
                    ThreadsPerFile = _profile.DownloadControlSettings.ThreadsPerFile;
                }

                Version = IncrementVersion(_profile.LastGeneratedVersion);
            }
            else
            {
                Version = "1.0.0";
            }

            StatusMessage = "配置已加载";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载发布者配置失败");
            StatusMessage = "加载失败";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ScanFolderAsync(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return;
        }

        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "开始扫描文件夹并计算哈希...";
        Log.Information("开始扫描文件夹并计算哈希");
        _currentFolder = folderPath;
        RootItems.Clear();
        FocusedItem = null;
        EditDownloadSegment = string.Empty;
        EditOverrideBaseUrl = null;

        try
        {
            var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
            var hashMap = await ComputeHashesAsync(files);
            BuildTree(folderPath, hashMap);
            StatusMessage = "扫描完成";
            ApplyToSelectedCommand.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "扫描文件夹失败");
            StatusMessage = "扫描失败";
        }
        finally
        {
            IsBusy = false;
            ApplyToSelectedCommand.RaiseCanExecuteChanged();
        }
    }

    public (OnlineManifest manifest, PublisherProfile profile) BuildManifest()
    {
        if (string.IsNullOrWhiteSpace(_currentFolder))
        {
            throw new InvalidOperationException("尚未选择文件夹");
        }

        var files = CollectSelectedFiles();
        if (files.Count == 0)
        {
            throw new InvalidOperationException("未选择任何文件");
        }
        var manifest = new OnlineManifest
        {
            Info = Info,
            Version = Version,
            BaseDownloadUrl = BaseDownloadUrl,
            AppendVersionToPath = AppendVersionToPath,
            LaunchExecutable = LaunchExecutable,
            DownloadControl = new DownloadControlConfig
            {
                EnableMultiFileDownload = EnableMultiFileDownload,
                MaxConcurrentFiles = Math.Max(1, MaxConcurrentFiles),
                EnableMultiThreadDownload = EnableMultiThreadDownload,
                ThreadsPerFile = Math.Max(1, ThreadsPerFile)
            },
            Files = files
        };

        Log.Information("开始生成 online_manifest.json");

        var profile = new PublisherProfile
        {
            LastGeneratedVersion = Version,
            Info = Info,
            BaseDownloadUrl = BaseDownloadUrl,
            AppendVersionToPath = AppendVersionToPath,
            LaunchExecutable = LaunchExecutable,
            DownloadControlSettings = new DownloadControlConfig
            {
                EnableMultiFileDownload = EnableMultiFileDownload,
                MaxConcurrentFiles = Math.Max(1, MaxConcurrentFiles),
                EnableMultiThreadDownload = EnableMultiThreadDownload,
                ThreadsPerFile = Math.Max(1, ThreadsPerFile)
            }
        };

        Log.Information("保存配置到 publisher_profile.json");

        return (manifest, profile);
    }

    public async Task PersistProfileAsync(PublisherProfile profile)
    {
        await _configService.SavePublisherProfileAsync(profile);
    }

    private async Task<Dictionary<string, string>> ComputeHashesAsync(IReadOnlyList<string> files)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await Parallel.ForEachAsync(files, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, async (file, _) =>
        {
            var relative = Normalize(Path.GetRelativePath(_currentFolder!, file));
            var hash = await _hashService.ComputeFileHashAsync(file);
            lock (result)
            {
                result[relative] = hash;
            }
        });

        return result;
    }

    private void BuildTree(string folderPath, IReadOnlyDictionary<string, string> hashMap)
    {
        var root = new PublishFileItem
        {
            Name = new DirectoryInfo(folderPath).Name,
            RelativePath = string.Empty,
            IsDirectory = true,
            IsSelected = true
        };

        foreach (var entry in hashMap.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            AddNode(root, entry.Key, entry.Value);
        }

        RootItems.Add(root);
    }

    private void AddNode(PublishFileItem parent, string relativePath, string hash)
    {
        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = parent;
        var pathBuilder = new List<string>();
        for (var i = 0; i < segments.Length; i++)
        {
            pathBuilder.Add(segments[i]);
            var partialPath = string.Join('/', pathBuilder);
            var existing = current.Children.FirstOrDefault(c => string.Equals(c.Name, segments[i], StringComparison.OrdinalIgnoreCase));
            if (i == segments.Length - 1)
            {
                if (existing == null)
                {
                    var item = new PublishFileItem
                    {
                        Name = segments[i],
                        RelativePath = partialPath,
                        Hash = hash,
                        IsDirectory = false,
                        DownloadSegment = segments[i]
                    };
                    current.Children.Add(item);
                }
                else
                {
                    existing.Hash = hash;
                    existing.DownloadSegment = segments[i];
                }
            }
            else
            {
                if (existing == null)
                {
                    existing = new PublishFileItem
                    {
                        Name = segments[i],
                        RelativePath = partialPath,
                        IsDirectory = true
                    };
                    current.Children.Add(existing);
                }

                current = existing;
            }
        }
    }

    private List<ManifestFileEntry> CollectSelectedFiles()
    {
        var list = new List<ManifestFileEntry>();
        foreach (var root in RootItems)
        {
            CollectRecursive(root, list);
        }

        return list;
    }

    private void CollectRecursive(PublishFileItem item, List<ManifestFileEntry> target)
    {
        if (item.IsDirectory)
        {
            foreach (var child in item.Children)
            {
                CollectRecursive(child, target);
            }

            return;
        }

        if (!item.IsSelected)
        {
            return;
        }

        target.Add(new ManifestFileEntry
        {
            RelativePath = item.RelativePath,
            Hash = item.Hash,
            DownloadSegment = string.IsNullOrWhiteSpace(item.DownloadSegment) ? Path.GetFileName(item.RelativePath) : item.DownloadSegment,
            OverrideBaseUrl = item.OverrideBaseUrl
        });
    }

    public void SetFocusedItem(PublishFileItem? item)
    {
        FocusedItem = item;
        if (item is { IsDirectory: false })
        {
            EditDownloadSegment = item.DownloadSegment;
            EditOverrideBaseUrl = item.OverrideBaseUrl;
        }
        else
        {
            EditDownloadSegment = string.Empty;
            EditOverrideBaseUrl = null;
        }
    }

    private void ApplyToCurrent()
    {
        if (FocusedItem is not { IsDirectory: false } file)
        {
            StatusMessage = "请选择文件";
            return;
        }

        var segment = string.IsNullOrWhiteSpace(EditDownloadSegment) ? Path.GetFileName(file.RelativePath) : EditDownloadSegment;
        file.DownloadSegment = segment;
        file.OverrideBaseUrl = string.IsNullOrWhiteSpace(EditOverrideBaseUrl) ? null : EditOverrideBaseUrl;
        StatusMessage = "已更新当前文件属性";
    }

    private void ApplyToSelectedFiles()
    {
        var targets = EnumerateFiles(true).ToList();
        if (targets.Count == 0)
        {
            StatusMessage = "未选择任何文件";
            return;
        }

        foreach (var file in targets)
        {
            var segment = string.IsNullOrWhiteSpace(EditDownloadSegment) ? Path.GetFileName(file.RelativePath) : EditDownloadSegment;
            file.DownloadSegment = segment;
            file.OverrideBaseUrl = string.IsNullOrWhiteSpace(EditOverrideBaseUrl) ? null : EditOverrideBaseUrl;
        }

        StatusMessage = "已批量更新文件属性";
    }

    private IEnumerable<PublishFileItem> EnumerateFiles(bool onlySelected)
    {
        foreach (var root in RootItems)
        {
            foreach (var file in EnumerateRecursive(root, onlySelected))
            {
                yield return file;
            }
        }
    }

    private IEnumerable<PublishFileItem> EnumerateRecursive(PublishFileItem item, bool onlySelected)
    {
        if (!item.IsDirectory)
        {
            if (!onlySelected || item.IsSelected)
            {
                yield return item;
            }

            yield break;
        }

        foreach (var child in item.Children)
        {
            foreach (var result in EnumerateRecursive(child, onlySelected))
            {
                yield return result;
            }
        }
    }

    private static string IncrementVersion(string version)
    {
        if (System.Version.TryParse(version, out var parsed))
        {
            var major = parsed.Major < 0 ? 1 : parsed.Major;
            var minor = parsed.Minor < 0 ? 0 : parsed.Minor;
            var build = parsed.Build < 0 ? 0 : parsed.Build + 1;
            return new System.Version(major, minor, build).ToString();
        }

        return "1.0.1";
    }

    private static string Normalize(string value)
    {
        return value.Replace('\\', '/');
    }
}

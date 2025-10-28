using System;
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

public sealed class LocalFilesViewModel : ObservableObject
{
    private readonly ConfigService _configService;
    private readonly FileScannerService _fileScannerService;
    private LocalConfig? _localConfig;
    private bool _isBusy;
    private string _statusMessage = string.Empty;

    public LocalFilesViewModel(ConfigService configService, FileScannerService fileScannerService)
    {
        _configService = configService;
        _fileScannerService = fileScannerService;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
    }

    public ObservableCollection<LocalFileNode> Roots { get; } = new();

    public AsyncRelayCommand RefreshCommand { get; }

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
            RefreshCommand.RaiseCanExecuteChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public async Task InitializeAsync()
    {
        await RefreshAsync();
    }

    public async Task AddIgnoreAsync(LocalFileNode node)
    {
        if (_localConfig == null)
        {
            return;
        }

        var relative = NormalizeRelative(Path.GetRelativePath(AppContext.BaseDirectory, node.FullPath));
        if (node.IsDirectory)
        {
            relative = relative.TrimEnd('/') + "/*";
        }

        if (_localConfig.IgnorePatterns.Any(p => string.Equals(NormalizeRelative(p), relative, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = "已存在相同的忽略规则";
            return;
        }

        Log.Information("添加 {Path} 到忽略列表", relative);
        _localConfig.IgnorePatterns.Add(relative);
        await _configService.SaveLocalConfigAsync(_localConfig);
        await RefreshAsync();
        StatusMessage = $"已忽略 {relative}";
    }

    private async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "正在扫描本地文件...";
        try
        {
            _localConfig = await _configService.LoadLocalConfigAsync();
            Roots.Clear();
            if (_localConfig == null)
            {
                StatusMessage = "未找到本地配置";
                return;
            }

            var nodes = await _fileScannerService.BuildTreeAsync(_localConfig);
            foreach (var node in nodes)
            {
                Roots.Add(node);
            }

            StatusMessage = "扫描完成";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "扫描本地文件失败");
            StatusMessage = "扫描失败";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string NormalizeRelative(string value)
    {
        return value.Replace('\\', '/');
    }
}

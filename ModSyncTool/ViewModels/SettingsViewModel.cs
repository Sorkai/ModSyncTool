using System;
using System.Threading.Tasks;
using ModSyncTool.Commands;
using ModSyncTool.Helpers;
using ModSyncTool.Models;
using ModSyncTool.Services;
using Serilog;

namespace ModSyncTool.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly ConfigService _configService;
    private readonly IDialogService _dialogService;

    private LocalConfig? _localConfig;
    private string _launchExecutable = string.Empty;
    private string _updateUrl = string.Empty;
    private bool _isCustomDownloadSettings;
    private bool _enableMultiFileDownload;
    private int _maxConcurrentFiles = 10;
    private bool _enableMultiThreadDownload;
    private int _threadsPerFile = 8;
    private bool _ignoreSslErrors;

    public SettingsViewModel(ConfigService configService, IDialogService dialogService)
    {
        _configService = configService;
        _dialogService = dialogService;
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => true);
        CancelCommand = new RelayCommand(() => RequestClose?.Invoke(this, EventArgs.Empty));
    }

    public event EventHandler? RequestClose;

    public AsyncRelayCommand SaveCommand { get; }

    public RelayCommand CancelCommand { get; }

    public string LaunchExecutable
    {
        get => _launchExecutable;
        set => SetProperty(ref _launchExecutable, value);
    }

    public string UpdateUrl
    {
        get => _updateUrl;
        set => SetProperty(ref _updateUrl, value);
    }

    public bool IsCustomDownloadSettings
    {
        get => _isCustomDownloadSettings;
        set => SetProperty(ref _isCustomDownloadSettings, value);
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

    public bool IgnoreSslErrors
    {
        get => _ignoreSslErrors;
        set => SetProperty(ref _ignoreSslErrors, value);
    }

    public async Task InitializeAsync()
    {
        _localConfig = await _configService.LoadLocalConfigAsync() ?? new LocalConfig();
        LaunchExecutable = _localConfig.LaunchExecutable;
        UpdateUrl = _localConfig.UpdateUrl ?? string.Empty;
        IgnoreSslErrors = _localConfig.IgnoreSslErrors;
        if (_localConfig.DownloadControl != null)
        {
            IsCustomDownloadSettings = true;
            EnableMultiFileDownload = _localConfig.DownloadControl.EnableMultiFileDownload;
            MaxConcurrentFiles = _localConfig.DownloadControl.MaxConcurrentFiles;
            EnableMultiThreadDownload = _localConfig.DownloadControl.EnableMultiThreadDownload;
            ThreadsPerFile = _localConfig.DownloadControl.ThreadsPerFile;
        }
        else
        {
            IsCustomDownloadSettings = false;
            EnableMultiFileDownload = true;
            MaxConcurrentFiles = 10;
            EnableMultiThreadDownload = false;
            ThreadsPerFile = 8;
        }
    }

    private async Task SaveAsync()
    {
        if (_localConfig == null)
        {
            _localConfig = new LocalConfig();
        }

        _localConfig.LaunchExecutable = LaunchExecutable;
        _localConfig.UpdateUrl = UpdateUrl;
        _localConfig.IgnoreSslErrors = IgnoreSslErrors;
        if (IsCustomDownloadSettings)
        {
            var maxFiles = Math.Max(1, MaxConcurrentFiles);
            var threads = Math.Max(1, ThreadsPerFile);
            _localConfig.DownloadControl = new DownloadControlConfig
            {
                EnableMultiFileDownload = EnableMultiFileDownload,
                MaxConcurrentFiles = maxFiles,
                EnableMultiThreadDownload = EnableMultiThreadDownload,
                ThreadsPerFile = threads
            };
        }
        else
        {
            _localConfig.DownloadControl = null;
        }

        await _configService.SaveLocalConfigAsync(_localConfig);
        Log.Information("设置已保存");

        // 友好提示：如果未配置 UpdateUrl，则提醒用户更新仍将禁用
        if (string.IsNullOrWhiteSpace(_localConfig.UpdateUrl))
        {
            _dialogService.ShowInfo("提示", "尚未配置远程链接，更新功能将保持禁用。您可以稍后在设置中补充远程链接。");
        }
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}

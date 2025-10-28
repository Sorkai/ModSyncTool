using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ModSyncTool.Commands;
using ModSyncTool.Helpers;
using ModSyncTool.Models;
using ModSyncTool.Services;
using Serilog;

namespace ModSyncTool.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ConfigService _configService;
    private readonly SyncService _syncService;
    private readonly IDialogService _dialogService;
    private readonly FileScannerService _fileScannerService;
    private readonly HashService _hashService;

    private CancellationTokenSource? _updateCts;
    private bool _isBusy;
    private string _statusText = "就绪";
    private bool _isProgressVisible;
    private double _progressValue;
    private bool _isUpdateEnabled;
    private bool _isManageLocalEnabled;
    private bool _isAdminMode;
    private string? _notificationText;

    public MainViewModel(ConfigService configService, SyncService syncService, IDialogService dialogService, FileScannerService fileScannerService, HashService hashService)
    {
        _configService = configService;
        _syncService = syncService;
        _dialogService = dialogService;
        _fileScannerService = fileScannerService;
        _hashService = hashService;

        UpdateAndLaunchCommand = new AsyncRelayCommand(ExecuteUpdateAndLaunchAsync, () => IsUpdateEnabled && !IsBusy);
        OpenSettingsCommand = new RelayCommand(OpenSettings, () => !IsBusy);
        OpenLocalFilesCommand = new RelayCommand(OpenLocalFiles, () => IsManageLocalEnabled && !IsBusy);
        OpenManifestGeneratorCommand = new RelayCommand(OpenManifestGenerator, () => !IsBusy);
    }

    public AsyncRelayCommand UpdateAndLaunchCommand { get; }

    public RelayCommand OpenSettingsCommand { get; }

    public RelayCommand OpenLocalFilesCommand { get; }

    public RelayCommand OpenManifestGeneratorCommand { get; }

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
            UpdateAndLaunchCommand.RaiseCanExecuteChanged();
            OpenSettingsCommand.RaiseCanExecuteChanged();
            OpenLocalFilesCommand.RaiseCanExecuteChanged();
            OpenManifestGeneratorCommand.RaiseCanExecuteChanged();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsProgressVisible
    {
        get => _isProgressVisible;
        private set => SetProperty(ref _isProgressVisible, value);
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set => SetProperty(ref _progressValue, value);
    }

    public bool IsUpdateEnabled
    {
        get => _isUpdateEnabled;
        private set
        {
            if (_isUpdateEnabled == value)
            {
                return;
            }

            _isUpdateEnabled = value;
            OnPropertyChanged();
            UpdateAndLaunchCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsManageLocalEnabled
    {
        get => _isManageLocalEnabled;
        private set
        {
            if (_isManageLocalEnabled == value)
            {
                return;
            }

            _isManageLocalEnabled = value;
            OnPropertyChanged();
            OpenLocalFilesCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsAdminMode
    {
        get => _isAdminMode;
        private set => SetProperty(ref _isAdminMode, value);
    }

    public string? NotificationText
    {
        get => _notificationText;
        private set => SetProperty(ref _notificationText, value);
    }

    public async Task InitializeAsync()
    {
        try
        {
            StatusText = "初始化中...";
            NotificationText = null;
            var skip = await _configService.SkipFlagExistsAsync();

            // 加载本地配置（可能不存在）
            var local = await _configService.LoadLocalConfigAsync();

            if (skip)
            {
                Log.Information("检测到管理员模式 flag，跳过配置检查");
                IsAdminMode = true;
                IsUpdateEnabled = false;
                IsManageLocalEnabled = false;
                StatusText = "管理员模式";
                NotificationText = "管理员模式已启用，更新功能不可用。";
                return;
            }

            // 初始化判断逻辑：以配置内容为依据，而非仅以文件是否存在
            // 认为存在非空 UpdateUrl 才视为已配置可更新
            var configured = !string.IsNullOrWhiteSpace(local?.UpdateUrl);
            if (!configured)
            {
                await HandleFirstRunAsync();
                return;
            }

            IsUpdateEnabled = true;
            IsManageLocalEnabled = true;
            StatusText = "就绪";
            NotificationText = null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "初始化失败");
            StatusText = "初始化失败";
            _dialogService.ShowError("错误", ex.Message);
        }
    }

    private async Task HandleFirstRunAsync()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            System.Windows.Application.Current.MainWindow?.Activate();
        });

        var choice = _dialogService.ShowWelcomeDialog();
        switch (choice)
        {
            case WelcomeDialogChoice.InputRemoteUrl:
                var url = _dialogService.PromptForRemoteUrl();
                if (string.IsNullOrWhiteSpace(url))
                {
                    StatusText = "未提供远程配置";
                    IsUpdateEnabled = false;
                    IsManageLocalEnabled = false;
                    NotificationText = "请在设置中手动配置远程链接后再尝试同步。";
                    return;
                }

                try
                {
                    var config = await _syncService.InitializeFromRemoteAsync(url!);
                    NotificationText = "已根据远程配置自动设置启动程序。如若无法启动，请在设置中手动指定。";
                    IsUpdateEnabled = true;
                    IsManageLocalEnabled = true;
                    StatusText = "首次初始化完成，准备更新";
                    await ExecuteUpdateAndLaunchAsync();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "首次初始化失败");
                    _dialogService.ShowError("初始化失败", ex.Message);
                    IsUpdateEnabled = false;
                    IsManageLocalEnabled = false;
                }

                break;

            case WelcomeDialogChoice.SkipOnce:
                Log.Information("用户选择本次忽略配置检测");
                IsUpdateEnabled = false;
                IsManageLocalEnabled = false;
                StatusText = "已忽略配置检测";
                NotificationText = "更新功能已禁用。本次会话仅可使用管理相关功能。";
                break;

            case WelcomeDialogChoice.SkipPermanently:
                Log.Information("用户选择永久忽略配置检测");
                await _configService.CreateSkipFlagAsync();
                IsUpdateEnabled = false;
                IsManageLocalEnabled = false;
                StatusText = "已永久忽略检测";
                NotificationText = "已创建管理员模式标记文件。若需恢复，请删除 skip_config_check.flag。";
                break;

            default:
                IsUpdateEnabled = false;
                IsManageLocalEnabled = false;
                StatusText = "初始化已取消";
                NotificationText = "已取消初始化流程。您可以稍后在设置中配置远程链接。";
                break;
        }
    }

    private async Task ExecuteUpdateAndLaunchAsync()
    {
        if (!IsUpdateEnabled || IsBusy)
        {
            return;
        }

        IsBusy = true;
        IsProgressVisible = true;
        ProgressValue = 0;
        StatusText = "正在检查更新...";
        _updateCts = new CancellationTokenSource();

        var statusProgress = new Progress<string>(message => StatusText = message);
        var progress = new Progress<double?>(value =>
        {
            if (value.HasValue)
            {
                ProgressValue = value.Value * 100.0;
            }
        });

        try
        {
            var result = await _syncService.RunUpdateAndLaunchAsync(statusProgress, progress, _updateCts.Token);
            await HandleSyncResultAsync(result);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "更新流程失败");
            _dialogService.ShowError("更新失败", ex.Message);
            StatusText = "更新失败";
        }
        finally
        {
            _updateCts?.Dispose();
            _updateCts = null;
            IsProgressVisible = false;
            IsBusy = false;
            ProgressValue = 0;
        }
    }

    private async Task HandleSyncResultAsync(SyncExecutionResult result)
    {
        if (!result.Success)
        {
            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                _dialogService.ShowError("错误", result.ErrorMessage);
                NotificationText = result.ErrorMessage;
            }

            return;
        }

        IsManageLocalEnabled = true;
        NotificationText = null;

        if (result.RequiresLaunchDecision)
        {
            if (result.RemoteManifest == null || result.UpdatedLocalConfig == null)
            {
                StatusText = "启动项不匹配";
                NotificationText = "远程启动项信息不完整，自动启动已取消。";
                return;
            }

            var decision = _dialogService.ShowLaunchMismatchDialog(result.RemoteLaunchExecutable ?? string.Empty, result.RemoteVersion ?? string.Empty);
            if (decision == LaunchMismatchDecision.IgnorePermanently)
            {
                Log.Information("用户选择永久忽略 {Version} 的启动项提示", result.RemoteManifest.Version);
                result.UpdatedLocalConfig.IgnoredRemoteLaunchVersion = result.RemoteManifest.Version;
                await _configService.SaveLocalConfigAsync(result.UpdatedLocalConfig);
                NotificationText = $"已永久忽略版本 {result.RemoteManifest.Version} 的启动项提示。";
            }
            else if (decision == LaunchMismatchDecision.IgnoreOnce)
            {
                Log.Information("用户选择本次忽略启动项提示");
                NotificationText = "已忽略本次启动项提示。";
            }
            else
            {
                StatusText = "启动项不匹配";
                NotificationText = "启动项未更新，已取消自动启动。";
                return;
            }

            var launchedAfterDecision = await _syncService.LaunchApplicationAsync(result.UpdatedLocalConfig);
            if (!launchedAfterDecision)
            {
                StatusText = "启动失败";
                _dialogService.ShowWarning("提示", "更新已完成，但未能启动程序。请检查启动项设置。");
                NotificationText = "更新完成，但启动程序缺失或无法执行。";
            }
            else
            {
                StatusText = "已启动";
                NotificationText = null;
            }

            return;
        }

        if (!result.AppLaunched)
        {
            StatusText = "启动失败";
            _dialogService.ShowWarning("提示", "更新已完成，但未能启动程序。请检查启动项设置。");
            NotificationText = "更新完成，但启动程序缺失或无法执行。";
        }
        else
        {
            StatusText = "已启动";
            NotificationText = null;
        }
    }

    private void OpenSettings()
    {
        var viewModel = new SettingsViewModel(_configService, _dialogService);
        var window = new Views.SettingsWindow(viewModel)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        window.ShowDialog();
    }

    private void OpenLocalFiles()
    {
        var viewModel = new LocalFilesViewModel(_configService, _fileScannerService);
        var window = new Views.LocalFilesWindow(viewModel)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        window.ShowDialog();
    }

    private void OpenManifestGenerator()
    {
        var viewModel = new ManifestGeneratorViewModel(_configService, _hashService);
        var window = new Views.ManifestGeneratorWindow(viewModel)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        window.ShowDialog();
    }
}

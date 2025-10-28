using System.Windows;
using ModSyncTool.Services;
using ModSyncTool.ViewModels;
using ModSyncTool.Views;
using Serilog;

namespace ModSyncTool;

public partial class App : System.Windows.Application
{
    public App()
    {
        // 捕获未处理异常，尽可能记录并给出提示
        this.DispatcherUnhandledException += (_, e) =>
        {
            Log.Fatal(e.Exception, "UI 线程未处理异常");
            System.Windows.MessageBox.Show(e.Exception.Message, "未处理异常", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                Log.Fatal(ex, "非 UI 线程未处理异常");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Fatal(e.Exception, "任务未观察到的异常");
            e.SetObserved();
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            LogService.Initialize();
            Log.Information("程序启动");

            // 跟随系统主题与强调色
            ThemeService.Initialize();

            var configService = new ConfigService();
            var hashService = new HashService();
            var fileScannerService = new FileScannerService();
            var dialogService = new DialogService();
            var syncService = new SyncService(configService, hashService, fileScannerService);

            var mainViewModel = new MainViewModel(configService, syncService, dialogService, fileScannerService, hashService);
            var mainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            // 明确指定主窗口与关闭策略，避免窗口未正确显示/立即退出
            this.ShutdownMode = ShutdownMode.OnMainWindowClose;
            this.MainWindow = mainWindow;
            Log.Debug("准备显示主窗口");
            mainWindow.Show();
            Log.Debug("主窗口已显示");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "应用启动失败");
            System.Windows.MessageBox.Show(ex.Message, "启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        LogService.Flush();
        base.OnExit(e);
    }
}

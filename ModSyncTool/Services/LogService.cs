using System.IO;
using Serilog;
using Serilog.Events;

namespace ModSyncTool.Services;

public static class LogService
{
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logDirectory);
        var logPath = Path.Combine(logDirectory, "modsync.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .WriteTo.Async(a => a.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7))
            .CreateLogger();

        _initialized = true;
    }

    public static void Flush()
    {
        if (!_initialized)
        {
            return;
        }

        Log.CloseAndFlush();
        _initialized = false;
    }
}

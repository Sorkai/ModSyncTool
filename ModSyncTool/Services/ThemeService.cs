using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using System.Runtime.Versioning;
using Serilog;

namespace ModSyncTool.Services;

[SupportedOSPlatform("windows")]
public static class ThemeService
{
    private static ResourceDictionary? _themeDictionary;

    public static void Initialize()
    {
        ApplyThemeFromSystem();
        TryApplyAccentFromSystem();
        SystemParameters.StaticPropertyChanged += (_, __) =>
        {
            // 当系统参数变化时，尝试刷新强调色
            TryApplyAccentFromSystem();
        };

        // 监听用户偏好变化（包括主题切换）
        SystemEvents.UserPreferenceChanged += (_, __) =>
        {
            ApplyThemeFromSystem();
            TryApplyAccentFromSystem();
        };
    }

    public static void ApplyThemeFromSystem()
    {
        bool useLight = IsSystemLightTheme();
        Log.Information("ThemeService: applying {Theme} theme", useLight ? "Light" : "Dark");
        var uri = new Uri(useLight
            ? "pack://application:,,,/ModSyncTool;component/Themes/Light.xaml"
            : "pack://application:,,,/ModSyncTool;component/Themes/Dark.xaml", UriKind.Absolute);

        var dict = new ResourceDictionary { Source = uri };
        var app = System.Windows.Application.Current;
        if (app == null) return;

        // 移除旧的主题字典
        var existing = app.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("/Themes/Light.xaml") ||
                                  d.Source != null && d.Source.OriginalString.Contains("/Themes/Dark.xaml"));
        if (existing != null)
        {
            app.Resources.MergedDictionaries.Remove(existing);
        }

        app.Resources.MergedDictionaries.Add(dict);
        _themeDictionary = dict;
    }

    public static void TryApplyAccentFromSystem()
    {
        var app = System.Windows.Application.Current;
        if (app == null) return;

        // 近似跟随系统强调色：使用 WindowGlassBrush 作为参考。
        var glass = SystemParameters.WindowGlassBrush as SolidColorBrush;
        if (glass == null) return;
        var baseColor = glass.Color;

        SetBrush("AccentBrush", new SolidColorBrush(baseColor));
        SetBrush("AccentBrushHover", new SolidColorBrush(Darken(baseColor, 0.08)));
        SetBrush("AccentBrushPressed", new SolidColorBrush(Darken(baseColor, 0.16)));
    }

    private static void SetBrush(string key, SolidColorBrush brush)
    {
        brush.Freeze();
        System.Windows.Application.Current.Resources[key] = brush;
    }

    private static bool IsSystemLightTheme()
    {
        try
        {
            // 0 = Dark, 1 = Light
            const string personalizePath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

            using (var key = Registry.CurrentUser.OpenSubKey(personalizePath))
            {
                object? value = key?.GetValue("AppsUseLightTheme", null);
                if (value is int i)
                {
                    Log.Debug("ThemeService: registry AppsUseLightTheme={Value}", i);
                    return i != 0;
                }

                // 兼容某些系统只设置 SystemUsesLightTheme 的情况
                value = key?.GetValue("SystemUsesLightTheme", 1);
                if (value is int j)
                {
                    Log.Debug("ThemeService: registry SystemUsesLightTheme={Value}", j);
                    return j != 0;
                }
            }
        }
        catch
        {
            // ignore
        }
        return true;
    }

    private static System.Windows.Media.Color Darken(System.Windows.Media.Color color, double amount)
    {
        byte Clamp(double v) => (byte)Math.Max(0, Math.Min(255, v));
        return System.Windows.Media.Color.FromArgb(color.A,
            Clamp(color.R * (1 - amount)),
            Clamp(color.G * (1 - amount)),
            Clamp(color.B * (1 - amount)));
    }
}

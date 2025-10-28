using System;
using System.Globalization;
using System.Windows.Data;
using ModSyncTool.Models;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;

namespace ModSyncTool.Converters;

public sealed class LocalFileStatusBrushConverter : IValueConverter
{
    public MediaBrush ManagedBrush { get; set; } = MediaBrushes.ForestGreen;

    public MediaBrush IgnoredBrush { get; set; } = MediaBrushes.Gray;

    public MediaBrush UntrackedBrush { get; set; } = MediaBrushes.OrangeRed;

    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is LocalFileStatus status)
        {
            return status switch
            {
                LocalFileStatus.Managed => ManagedBrush,
                LocalFileStatus.Ignored => IgnoredBrush,
                _ => UntrackedBrush
            };
        }

        return UntrackedBrush;
    }

    public object ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

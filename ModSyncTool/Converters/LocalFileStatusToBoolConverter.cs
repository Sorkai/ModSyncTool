using System;
using System.Globalization;
using System.Windows.Data;
using ModSyncTool.Models;

namespace ModSyncTool.Converters;

public sealed class LocalFileStatusToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is LocalFileStatus status && status == LocalFileStatus.Untracked;
    }

    public object ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

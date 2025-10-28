using System.Windows;
using System.Windows.Controls;

namespace ModSyncTool.Helpers;

public static class TreeViewHelper
{
    public static readonly DependencyProperty DoubleClickCommandProperty = DependencyProperty.RegisterAttached(
        "DoubleClickCommand",
        typeof(System.Windows.Input.ICommand),
        typeof(TreeViewHelper),
        new PropertyMetadata(null, OnDoubleClickCommandChanged));

    public static void SetDoubleClickCommand(DependencyObject element, System.Windows.Input.ICommand? value)
    {
        element.SetValue(DoubleClickCommandProperty, value);
    }

    public static System.Windows.Input.ICommand? GetDoubleClickCommand(DependencyObject element)
    {
        return (System.Windows.Input.ICommand?)element.GetValue(DoubleClickCommandProperty);
    }

    private static void OnDoubleClickCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TreeViewItem item)
        {
            return;
        }

        if (e.OldValue != null)
        {
            item.MouseDoubleClick -= OnTreeViewItemDoubleClick;
        }

        if (e.NewValue != null)
        {
            item.MouseDoubleClick += OnTreeViewItemDoubleClick;
        }
    }

    private static void OnTreeViewItemDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not TreeViewItem item)
        {
            return;
        }

        var command = GetDoubleClickCommand(item);
        if (command == null)
        {
            return;
        }

        if (!item.IsSelected)
        {
            return;
        }

        if (command.CanExecute(item.DataContext))
        {
            command.Execute(item.DataContext);
        }
    }
}

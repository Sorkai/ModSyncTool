using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ModSyncTool.Commands;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _executeAsync;
    private readonly Func<bool>? _canExecute;
    private bool _isRunning;

    public AsyncRelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
    {
        _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        if (_isRunning)
        {
            return false;
        }

        return _canExecute?.Invoke() ?? true;
    }

    public async void Execute(object? parameter)
    {
        if (_isRunning)
        {
            return;
        }

        try
        {
            _isRunning = true;
            RaiseCanExecuteChanged();
            // 保持在 UI 上下文恢复，避免后续引发 UI 线程访问异常
            await _executeAsync();
        }
        finally
        {
            _isRunning = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged()
    {
        // 始终在 UI 线程上触发，以避免 ButtonBase.UpdateCanExecute 引发跨线程异常
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher?.CheckAccess() == true)
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            app?.Dispatcher?.BeginInvoke(new Action(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty)));
        }
    }
}

using System;
using System.Windows.Input;

namespace Img2SE2;

public class SimpleCommand(Action action) : ICommand
{
    private readonly Action? _action = action;
    private bool _isEnabled = true;
    
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool CanExecute(object? parameter) => IsEnabled && _action != null;

    public void Execute(object? parameter = null) => _action?.Invoke();

    public event EventHandler? CanExecuteChanged;
}

public class SimpleCommand<T>(Action<T> action) : ICommand
{
    private readonly Action<T>? _action = action;
    private bool _isEnabled = true;
    
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            _isEnabled = value;
        }
    }

    public bool CanExecute(object? parameter) => IsEnabled && _action != null;
    
    public void Execute(object? parameter)
    {
        if(parameter is T t)
            _action?.Invoke(t);
    }

    public event EventHandler? CanExecuteChanged;
}
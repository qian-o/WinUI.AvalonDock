// Adapted from Dirkster.AvalonDock v5.0.0 (MS-PL), Commands/RelayCommand.cs.
// Upstream: 408dc2896e2f41f3bb79a15207f160edee8a6792.
using System;
using System.Windows.Input;

namespace AvalonDock.Commands;

/// <summary>布局项和浮动窗口使用的命令。</summary>
internal class RelayCommand<T> : ICommand
{
    private readonly WeakCommandHandler<Action<T>> execute;
    private readonly WeakCommandHandler<Func<T, bool>>? canExecute;

    public RelayCommand(Action<T> execute) : this(execute, null)
    {
    }

    public RelayCommand(Action<T> execute, Func<T, bool>? canExecute)
    {
        this.execute = new WeakCommandHandler<Action<T>>(execute ?? throw new ArgumentNullException(nameof(execute)));
        if (canExecute != null)
        {
            this.canExecute = new WeakCommandHandler<Func<T, bool>>(canExecute);
        }
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        if (canExecute == null)
        {
            return true;
        }
        Func<T, bool>? predicate = canExecute.GetHandler();
        if (predicate == null)
        {
            return false;
        }
        if (parameter == null && typeof(T).IsValueType)
        {
            return predicate(default!);
        }
        return (parameter == null || parameter is T) && predicate((T)parameter!);
    }

    public virtual void Execute(object? parameter)
    {
        object? value = parameter;
        if (value != null && value.GetType() != typeof(T))
        {
            if (typeof(T).IsEnum)
            {
                value = Enum.Parse(typeof(T), value.ToString()!);
            }
            else if (value is IConvertible)
            {
                value = Convert.ChangeType(value, typeof(T), null);
            }
        }

        if (!CanExecute(value) || execute.GetHandler() is not { } action)
        {
            return;
        }
        if (value == null && typeof(T).IsValueType)
        {
            value = default(T);
        }

        if (execute.IsStatic)
        {
            action((T)value!);
        }
        else
        {
            // 沿用上游行为：旧视图命令触发的实例回调异常不向界面传播。
            try
            {
                action((T)value!);
            }
            catch
            {
            }
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

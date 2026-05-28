using System;
using System.Windows.Input;

namespace VHDX_Manager.ViewModels
{
    /// <summary>
    /// RelayCommand 实现
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        /// <summary>
        /// 初始化 RelayCommand
        /// </summary>
        /// <param name="execute">执行方法</param>
        /// <param name="canExecute">是否可执行</param>
        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// 初始化 RelayCommand（无参数版本）
        /// </summary>
        /// <param name="execute">执行方法</param>
        /// <param name="canExecute">是否可执行</param>
        public RelayCommand(Action execute, Func<bool>? canExecute = null)
            : this(_ => execute(), canExecute != null ? _ => canExecute() : null)
        {
        }

        /// <summary>
        /// 是否可执行变更事件
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        /// <summary>
        /// 判断是否可执行
        /// </summary>
        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        /// <summary>
        /// 执行命令
        /// </summary>
        public void Execute(object? parameter)
        {
            _execute(parameter);
        }

        /// <summary>
        /// 触发 CanExecuteChanged 事件
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>
    /// 泛型 RelayCommand 实现
    /// </summary>
    /// <typeparam name="T">参数类型</typeparam>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Predicate<T?>? _canExecute;

        /// <summary>
        /// 初始化 RelayCommand
        /// </summary>
        /// <param name="execute">执行方法</param>
        /// <param name="canExecute">是否可执行</param>
        public RelayCommand(Action<T?> execute, Predicate<T?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// 是否可执行变更事件
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        /// <summary>
        /// 判断是否可执行
        /// </summary>
        public bool CanExecute(object? parameter)
        {
            if (parameter is T typedParameter)
            {
                return _canExecute == null || _canExecute(typedParameter);
            }
            return _canExecute == null || _canExecute(default);
        }

        /// <summary>
        /// 执行命令
        /// </summary>
        public void Execute(object? parameter)
        {
            if (parameter is T typedParameter)
            {
                _execute(typedParameter);
            }
            else
            {
                _execute(default);
            }
        }

        /// <summary>
        /// 触发 CanExecuteChanged 事件
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
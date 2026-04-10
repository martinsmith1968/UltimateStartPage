using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace UltimateStartPage.Core.Mvvm
{
    /// <summary>
    /// Async ICommand implementation with parameter support. Wraps Func&lt;T, Task&gt;
    /// and executes via async void (standard WPF pattern). Prevents re-entrant execution.
    /// </summary>
    public sealed class AsyncRelayCommand<T> : ICommand where T : class
    {
        private readonly Func<T?, Task> _execute;
        private readonly Func<T?, bool>? _canExecute;
        private bool _isExecuting;

        public AsyncRelayCommand(Func<T?, Task> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public void RaiseCanExecuteChanged()
            => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

        public bool CanExecute(object? parameter)
            => !_isExecuting && (_canExecute == null || _canExecute((T?)parameter));

        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter))
                return;

            _isExecuting = true;
            RaiseCanExecuteChanged();
            try
            {
                await _execute((T?)parameter);
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }
    }
}

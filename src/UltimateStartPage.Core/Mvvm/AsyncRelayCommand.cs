using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace UltimateStartPage.Core.Mvvm
{
    /// <summary>
    /// Async ICommand implementation. Wraps Func&lt;Task&gt; and executes via async void
    /// (standard WPF pattern). Prevents re-entrant execution while running.
    /// </summary>
    public sealed class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool>? _canExecute;
        private bool _isExecuting;

        public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public void RaiseCanExecuteChanged()
            => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

        public bool CanExecute(object parameter)
            => !_isExecuting && (_canExecute == null || _canExecute());

        public async void Execute(object parameter)
        {
            if (!CanExecute(parameter))
                return;

            _isExecuting = true;
            RaiseCanExecuteChanged();
            try
            {
                await _execute();
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }
    }
}

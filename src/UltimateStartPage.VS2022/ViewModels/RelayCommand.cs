using System;
using System.Windows.Input;

namespace UltimateStartPage.VS2022.ViewModels
{
    /// <summary>
    /// Minimal ICommand implementation for stub ViewModels.
    /// McManus: replace with CommunityToolkit.Mvvm.Input.RelayCommand once ViewModels
    /// are migrated to Core and CommunityToolkit.Mvvm is added to Core dependencies.
    /// </summary>
    internal sealed class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute    = execute    ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add    => System.Windows.Input.CommandManager.RequerySuggested += value;
            remove => System.Windows.Input.CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute();

        public void Execute(object parameter) => _execute();
    }
}

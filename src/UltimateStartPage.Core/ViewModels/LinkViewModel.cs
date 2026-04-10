using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using UltimateStartPage.Core.Models;
using UltimateStartPage.Core.Mvvm;

namespace UltimateStartPage.Core.ViewModels
{
    /// <summary>
    /// ViewModel for a single solution/project link tile.
    /// </summary>
    public class LinkViewModel : ObservableObject
    {
        private string _name = string.Empty;
        private string _path = string.Empty;

        private readonly Func<LinkViewModel, Task> _onRemove;
        private readonly Action<string>? _openAction;
        private readonly RelayCommand _openCommand;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Path
        {
            get => _path;
            set
            {
                if (SetProperty(ref _path, value))
                    _openCommand.RaiseCanExecuteChanged();
            }
        }

        public ICommand OpenCommand => _openCommand;
        public ICommand RemoveCommand { get; }

        public LinkViewModel(SolutionLink model, Func<LinkViewModel, Task> onRemove, Action<string>? openAction = null)
        {
            _onRemove = onRemove ?? throw new ArgumentNullException(nameof(onRemove));
            _openAction = openAction;
            _name = model?.Name ?? string.Empty;
            _path = model?.FilePath ?? string.Empty;

            _openCommand = new RelayCommand(ExecuteOpen, () => !string.IsNullOrWhiteSpace(_path));
            RemoveCommand = new AsyncRelayCommand(ExecuteRemoveAsync);
        }

        public SolutionLink ToModel()
            => new SolutionLink(_name, _path);

        private void ExecuteOpen()
        {
            if (string.IsNullOrWhiteSpace(_path))
                return;

            // If a custom open action was provided (typically from VS2022 layer using EnvDTE),
            // use it. Otherwise, fall back to Process.Start which opens with the shell default.
            if (_openAction != null)
            {
                try
                {
                    _openAction(_path);
                }
                catch (Exception)
                {
                    // Silently swallow — UI feedback on open failure is a future concern.
                }
            }
            else
            {
                try
                {
                    Process.Start(new ProcessStartInfo(_path) { UseShellExecute = true });
                }
                catch (Exception)
                {
                    // Silently swallow — UI feedback on open failure is a future concern.
                }
            }
        }

        private async Task ExecuteRemoveAsync()
            => await _onRemove(this);
    }
}

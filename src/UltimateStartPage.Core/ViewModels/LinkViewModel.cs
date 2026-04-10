using System;
using System.Diagnostics;
using System.Threading.Tasks;
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

        public System.Windows.Input.ICommand OpenCommand => _openCommand;
        public System.Windows.Input.ICommand RemoveCommand { get; }

        public LinkViewModel(SolutionLink model, Func<LinkViewModel, Task> onRemove)
        {
            _onRemove = onRemove ?? throw new ArgumentNullException(nameof(onRemove));
            _name = model?.Name ?? string.Empty;
            _path = model?.FilePath ?? string.Empty;

            _openCommand = new RelayCommand(ExecuteOpen, () => !string.IsNullOrWhiteSpace(_path));
            RemoveCommand = new RelayCommand(ExecuteRemove);
        }

        public SolutionLink ToModel()
            => new SolutionLink(_name, _path);

        private void ExecuteOpen()
        {
            if (string.IsNullOrWhiteSpace(_path))
                return;

            // TODO (VS-layer): For opening .sln files inside VS, wire EnvDTE.Solution.Open()
            // or IVsSolution.OpenSolutionFile() via the VS service provider. That belongs in
            // UltimateStartPage.VS2022, not Core. Process.Start opens with the shell default.
            try
            {
                Process.Start(new ProcessStartInfo(_path) { UseShellExecute = true });
            }
            catch (Exception)
            {
                // Silently swallow — UI feedback on open failure is a future concern.
            }
        }

        private void ExecuteRemove()
            => _ = _onRemove(this);
    }
}

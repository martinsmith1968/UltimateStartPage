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
        private bool _isEditing;
        private string _editingName = string.Empty;
        private string _editingPath = string.Empty;

        private readonly Func<LinkViewModel, Task> _onRemove;
        private readonly Action<string>? _openAction;
        private readonly Func<Task> _saveCallback;
        private readonly RelayCommand _openCommand;

        public string Name
        {
            get => _name;
            set
            {
                if (SetProperty(ref _name, value))
                {
                    // Fire-and-forget save — exception handling deferred to logging framework
                    _ = _saveCallback();
                }
            }
        }

        public string Path
        {
            get => _path;
            set
            {
                if (SetProperty(ref _path, value))
                {
                    _openCommand.RaiseCanExecuteChanged();
                    // Fire-and-forget save — exception handling deferred to logging framework
                    _ = _saveCallback();
                }
            }
        }

        public bool IsEditing
        {
            get => _isEditing;
            set => SetProperty(ref _isEditing, value);
        }

        public string EditingName
        {
            get => _editingName;
            set => SetProperty(ref _editingName, value);
        }

        public string EditingPath
        {
            get => _editingPath;
            set => SetProperty(ref _editingPath, value);
        }

        public ICommand OpenCommand => _openCommand;
        public ICommand RemoveCommand { get; }
        public ICommand BeginEditCommand { get; }
        public ICommand CommitEditCommand { get; }
        public ICommand CancelEditCommand { get; }

        public LinkViewModel(SolutionLink model, Func<LinkViewModel, Task> onRemove, Func<Task> saveCallback, Action<string>? openAction = null)
        {
            _onRemove = onRemove ?? throw new ArgumentNullException(nameof(onRemove));
            _saveCallback = saveCallback ?? throw new ArgumentNullException(nameof(saveCallback));
            _openAction = openAction;
            _name = model?.Name ?? string.Empty;
            _path = model?.FilePath ?? string.Empty;

            _openCommand = new RelayCommand(ExecuteOpen, () => !string.IsNullOrWhiteSpace(_path));
            RemoveCommand = new AsyncRelayCommand(ExecuteRemoveAsync);
            BeginEditCommand = new RelayCommand(ExecuteBeginEdit);
            CommitEditCommand = new AsyncRelayCommand(ExecuteCommitEditAsync);
            CancelEditCommand = new RelayCommand(ExecuteCancelEdit);
        }

        private void ExecuteBeginEdit()
        {
            EditingName = _name;
            EditingPath = _path;
            IsEditing = true;
        }

        private async Task ExecuteCommitEditAsync()
        {
            IsEditing = false;
            // Setting Name and Path will trigger saves via property setters
            Name = EditingName;
            Path = EditingPath;
        }

        private void ExecuteCancelEdit()
        {
            IsEditing = false;
            EditingName = _name;
            EditingPath = _path;
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

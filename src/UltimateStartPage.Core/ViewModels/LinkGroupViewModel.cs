using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using UltimateStartPage.Core.Models;
using UltimateStartPage.Core.Mvvm;

namespace UltimateStartPage.Core.ViewModels
{
    /// <summary>
    /// ViewModel for a named group of solution/project link tiles.
    /// </summary>
    public class LinkGroupViewModel : ObservableObject
    {
        private string _name = string.Empty;
        private bool _isRenaming;
        private string _editingName = string.Empty;

        private readonly Func<LinkGroupViewModel, Task> _onRemove;
        private readonly Func<Task> _saveCallback;
        private readonly Action<string>? _openAction;

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

        public bool IsRenaming
        {
            get => _isRenaming;
            set => SetProperty(ref _isRenaming, value);
        }

        public string EditingName
        {
            get => _editingName;
            set => SetProperty(ref _editingName, value);
        }

        public ObservableCollection<LinkViewModel> Links { get; } = new ObservableCollection<LinkViewModel>();

        public ICommand AddLinkCommand { get; }
        public ICommand RemoveCommand { get; }
        public ICommand BeginRenameCommand { get; }
        public ICommand CommitRenameCommand { get; }
        public ICommand CancelRenameCommand { get; }

        public LinkGroupViewModel(LinkGroup model, Func<LinkGroupViewModel, Task> onRemove, Func<Task> saveCallback, Action<string>? openAction = null)
        {
            _onRemove = onRemove ?? throw new ArgumentNullException(nameof(onRemove));
            _saveCallback = saveCallback ?? throw new ArgumentNullException(nameof(saveCallback));
            _openAction = openAction;
            _name = model?.Name ?? string.Empty;

            if (model != null)
            {
                foreach (var link in model.Links)
                    Links.Add(new LinkViewModel(link, RemoveLinkAsync, _saveCallback, _openAction));
            }

            AddLinkCommand = new AsyncRelayCommand(ExecuteAddLinkAsync);
            RemoveCommand = new AsyncRelayCommand(ExecuteRemoveAsync);
            BeginRenameCommand = new RelayCommand(ExecuteBeginRename);
            CommitRenameCommand = new AsyncRelayCommand(ExecuteCommitRenameAsync);
            CancelRenameCommand = new RelayCommand(ExecuteCancelRename);
        }

        private void ExecuteBeginRename()
        {
            EditingName = _name;
            IsRenaming = true;
        }

        private async Task ExecuteCommitRenameAsync()
        {
            IsRenaming = false;
            // Setting Name will trigger save via property setter
            Name = EditingName;
        }

        private void ExecuteCancelRename()
        {
            IsRenaming = false;
            EditingName = _name;
        }

        /// <summary>Converts this ViewModel back to a domain model for persistence.</summary>
        public LinkGroup ToModel()
        {
            var group = new LinkGroup(_name);
            group.Links = Links.Select(l => l.ToModel()).ToList();
            return group;
        }

        private async Task ExecuteAddLinkAsync()
        {
            var newLink = new SolutionLink("New Link", string.Empty);
            Links.Add(new LinkViewModel(newLink, RemoveLinkAsync, _saveCallback, _openAction));
            await _saveCallback();
        }

        private async Task RemoveLinkAsync(LinkViewModel vm)
        {
            Links.Remove(vm);
            await _saveCallback();
        }

        private async Task ExecuteRemoveAsync()
            => await _onRemove(this);
    }
}

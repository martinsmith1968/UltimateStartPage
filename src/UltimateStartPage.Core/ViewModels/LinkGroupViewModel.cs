using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
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

        private readonly Func<LinkGroupViewModel, Task> _onRemove;
        private readonly Func<Task> _saveCallback;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public ObservableCollection<LinkViewModel> Links { get; } = new ObservableCollection<LinkViewModel>();

        public System.Windows.Input.ICommand AddLinkCommand { get; }
        public System.Windows.Input.ICommand RemoveCommand { get; }

        public LinkGroupViewModel(LinkGroup model, Func<LinkGroupViewModel, Task> onRemove, Func<Task> saveCallback)
        {
            _onRemove = onRemove ?? throw new ArgumentNullException(nameof(onRemove));
            _saveCallback = saveCallback ?? throw new ArgumentNullException(nameof(saveCallback));
            _name = model?.Name ?? string.Empty;

            if (model != null)
            {
                foreach (var link in model.Links)
                    Links.Add(new LinkViewModel(link, RemoveLinkAsync));
            }

            AddLinkCommand = new AsyncRelayCommand(ExecuteAddLinkAsync);
            RemoveCommand = new RelayCommand(ExecuteRemove);
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
            Links.Add(new LinkViewModel(newLink, RemoveLinkAsync));
            await _saveCallback();
        }

        private async Task RemoveLinkAsync(LinkViewModel vm)
        {
            Links.Remove(vm);
            await _saveCallback();
        }

        private void ExecuteRemove()
            => _ = _onRemove(this);
    }
}

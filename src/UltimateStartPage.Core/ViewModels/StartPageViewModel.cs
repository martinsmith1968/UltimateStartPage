using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using UltimateStartPage.Core.Models;
using UltimateStartPage.Core.Mvvm;
using UltimateStartPage.Core.Services;

namespace UltimateStartPage.Core.ViewModels
{
    /// <summary>
    /// Root ViewModel for the start page tool window.
    /// DataContext for StartPageToolWindowControl.
    /// </summary>
    public class StartPageViewModel : ObservableObject
    {
        private readonly ILinkRepository _repository;

        public ObservableCollection<LinkGroupViewModel> Groups { get; } =
            new ObservableCollection<LinkGroupViewModel>();

        /// <summary>True when at least one group exists — drives empty-state DataTrigger.</summary>
        public bool HasGroups => Groups.Count > 0;

        public ICommand AddGroupCommand { get; }

        public StartPageViewModel(ILinkRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            Groups.CollectionChanged += (_, __) => OnPropertyChanged(nameof(HasGroups));
            AddGroupCommand = new AsyncRelayCommand(ExecuteAddGroupAsync);
        }

        /// <summary>
        /// Loads groups from the repository and populates <see cref="Groups"/>.
        /// Call this from the tool window after construction.
        /// </summary>
        public async Task LoadAsync()
        {
            var groups = await _repository.GetGroupsAsync();
            Groups.Clear();
            foreach (var group in groups)
                Groups.Add(CreateGroupViewModel(group));
        }

        private async Task ExecuteAddGroupAsync()
        {
            var newGroup = new LinkGroup($"New Group {Groups.Count + 1}");
            Groups.Add(CreateGroupViewModel(newGroup));
            await SaveAsync();
        }

        private async Task RemoveGroupAsync(LinkGroupViewModel vm)
        {
            Groups.Remove(vm);
            await SaveAsync();
        }

        private async Task SaveAsync()
        {
            var models = Groups.Select(g => g.ToModel()).ToList();
            await _repository.SaveGroupsAsync(models);
        }

        private LinkGroupViewModel CreateGroupViewModel(LinkGroup group)
            => new LinkGroupViewModel(group, RemoveGroupAsync, SaveAsync);
    }
}

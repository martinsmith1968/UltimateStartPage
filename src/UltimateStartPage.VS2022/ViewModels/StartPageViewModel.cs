using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace UltimateStartPage.VS2022.ViewModels
{
    /// <summary>
    /// Root ViewModel for the start page tool window.
    /// DataContext for StartPageToolWindowControl.
    /// McManus: inject ILinkRepository and load Groups from Core on initialisation.
    /// </summary>
    public class StartPageViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<LinkGroupViewModel> Groups { get; } = new ObservableCollection<LinkGroupViewModel>();

        /// <summary>True when at least one group exists — drives empty-state visibility.</summary>
        public bool HasGroups => Groups.Count > 0;

        /// <summary>Opens the Add Group dialog/flow. Stub — McManus implements the body.</summary>
        public ICommand AddGroupCommand { get; }

        public StartPageViewModel()
        {
            AddGroupCommand = new RelayCommand(ExecuteAddGroup);
            Groups.CollectionChanged += (_, __) => OnPropertyChanged(nameof(HasGroups));
        }

        private void ExecuteAddGroup()
        {
            // TODO (McManus): prompt for group name and persist via ILinkRepository
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

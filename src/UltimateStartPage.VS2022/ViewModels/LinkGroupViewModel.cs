using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UltimateStartPage.VS2022.ViewModels
{
    /// <summary>
    /// Represents a named group of solution/project link tiles.
    /// McManus: populate from Core LinkGroup model via ILinkRepository.
    /// </summary>
    public class LinkGroupViewModel : INotifyPropertyChanged
    {
        private string _name = string.Empty;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public ObservableCollection<LinkViewModel> Links { get; } = new ObservableCollection<LinkViewModel>();

        public LinkGroupViewModel() { }

        public LinkGroupViewModel(string name)
        {
            _name = name;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

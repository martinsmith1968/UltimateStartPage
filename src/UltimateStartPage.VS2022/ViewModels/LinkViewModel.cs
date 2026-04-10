using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace UltimateStartPage.VS2022.ViewModels
{
    /// <summary>
    /// Represents a single solution/project link tile on the start page.
    /// McManus: wire OpenCommand to open the solution/project via DTE or IVsSolution.
    /// </summary>
    public class LinkViewModel : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _path = string.Empty;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Path
        {
            get => _path;
            set { _path = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Opens the solution or project file. Stub — McManus implements the body.
        /// </summary>
        public ICommand OpenCommand { get; }

        public LinkViewModel()
        {
            OpenCommand = new RelayCommand(ExecuteOpen, CanExecuteOpen);
        }

        public LinkViewModel(string name, string path) : this()
        {
            _name = name;
            _path = path;
        }

        private bool CanExecuteOpen() => !string.IsNullOrWhiteSpace(_path);

        private void ExecuteOpen()
        {
            // TODO (McManus): open via DTE or IVsUIShellOpenDocument
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

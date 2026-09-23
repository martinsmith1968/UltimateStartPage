using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using UltimateStartPage.Core.Models;

namespace UltimateStartPage.Core.ViewModels
{
    public sealed class SectionViewModel : ObservableObject
    {
        private readonly StartPageViewModel _root;
        private string _title;
        private bool _isCollapsed;
        private bool _isVisible = true;

        internal SectionViewModel(StartPageViewModel root, Section model)
        {
            _root = root;
            Id = model.Id;
            _title = model.Title;
            _isCollapsed = model.IsCollapsed;
            Links = new ObservableCollection<LinkViewModel>(model.Links.Select(l => new LinkViewModel(root, this, l)));
            Links.CollectionChanged += (_, __) => OnPropertyChanged(nameof(IsEmpty));

            AddLinkCommand = root.CreateCommand(_ => root.AddLinkAsync(this));
            RenameCommand = root.CreateCommand(_ => root.RenameSectionAsync(this));
            DeleteCommand = root.CreateCommand(_ => root.DeleteSectionAsync(this));
            MoveUpCommand = new RelayCommand(_ => root.MoveSection(this, -1));
            MoveDownCommand = new RelayCommand(_ => root.MoveSection(this, +1));
            ToggleCollapsedCommand = new RelayCommand(_ => IsCollapsed = !IsCollapsed);
        }

        public Guid Id { get; }

        public string Title
        {
            get => _title;
            set
            {
                if (SetProperty(ref _title, value))
                {
                    _root.RequestSave();
                }
            }
        }

        public bool IsCollapsed
        {
            get => _isCollapsed;
            set
            {
                if (SetProperty(ref _isCollapsed, value))
                {
                    _root.RequestSave();
                }
            }
        }

        public bool IsVisible
        {
            get => _isVisible;
            internal set => SetProperty(ref _isVisible, value);
        }

        public bool IsEmpty => Links.Count == 0;

        public ObservableCollection<LinkViewModel> Links { get; }

        public ICommand AddLinkCommand { get; }

        public ICommand RenameCommand { get; }

        public ICommand DeleteCommand { get; }

        public ICommand MoveUpCommand { get; }

        public ICommand MoveDownCommand { get; }

        public ICommand ToggleCollapsedCommand { get; }

        internal Section ToModel()
        {
            return new Section
            {
                Id = Id,
                Title = Title,
                IsCollapsed = IsCollapsed,
                Links = Links.Select(l => l.ToModel()).ToList(),
            };
        }
    }
}

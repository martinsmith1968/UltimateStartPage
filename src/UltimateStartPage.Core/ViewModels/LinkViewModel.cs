using System;
using System.Windows.Input;
using UltimateStartPage.Core.Models;

namespace UltimateStartPage.Core.ViewModels
{
    public sealed class LinkViewModel : ObservableObject
    {
        private readonly StartPageViewModel _root;
        private string _title;
        private string _target;
        private LinkKind _kind;
        private string? _description;
        private bool _isVisible = true;
        private bool _isMissing;

        internal LinkViewModel(StartPageViewModel root, SectionViewModel section, LinkItem model)
        {
            _root = root;
            Section = section;
            Id = model.Id;
            _title = model.Title;
            _target = model.Target;
            _kind = model.Kind;
            _description = model.Description;

            OpenCommand = root.CreateCommand(_ => root.OpenLinkAsync(this));
            EditCommand = root.CreateCommand(_ => root.EditLinkAsync(this));
            RemoveCommand = root.CreateCommand(_ => root.RemoveLinkAsync(this));
            OpenContainingFolderCommand = root.CreateCommand(_ => root.OpenContainingFolderAsync(this), _ => Kind != LinkKind.Url);
            MoveUpCommand = new RelayCommand(_ => root.MoveLink(this, -1));
            MoveDownCommand = new RelayCommand(_ => root.MoveLink(this, +1));
        }

        public Guid Id { get; }

        public SectionViewModel Section { get; internal set; }

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

        public string Target
        {
            get => _target;
            internal set => SetProperty(ref _target, value);
        }

        public LinkKind Kind
        {
            get => _kind;
            internal set => SetProperty(ref _kind, value);
        }

        public string? Description
        {
            get => _description;
            internal set
            {
                if (SetProperty(ref _description, value))
                {
                    OnPropertyChanged(nameof(ToolTip));
                }
            }
        }

        public string ToolTip => string.IsNullOrWhiteSpace(Description) ? Target : Description + Environment.NewLine + Target;

        public bool IsVisible
        {
            get => _isVisible;
            internal set => SetProperty(ref _isVisible, value);
        }

        /// <summary>The target could not be found on disk the last time links were checked.</summary>
        public bool IsMissing
        {
            get => _isMissing;
            internal set => SetProperty(ref _isMissing, value);
        }

        public ICommand OpenCommand { get; }

        public ICommand EditCommand { get; }

        public ICommand RemoveCommand { get; }

        public ICommand OpenContainingFolderCommand { get; }

        public ICommand MoveUpCommand { get; }

        public ICommand MoveDownCommand { get; }

        internal LinkItem ToModel()
        {
            return new LinkItem { Id = Id, Title = Title, Target = Target, Kind = Kind, Description = Description };
        }
    }
}

using System;
using System.Globalization;
using System.Windows.Input;
using UltimateStartPage.Core.Models;
using UltimateStartPage.Core.Services;

namespace UltimateStartPage.Core.ViewModels
{
    public sealed class RecentItemViewModel : ObservableObject
    {
        private bool _isVisible = true;

        internal RecentItemViewModel(StartPageViewModel root, RecentItem item, DateTimeOffset now)
        {
            FullPath = item.FullPath;
            Kind = item.Kind;
            IsFavorite = item.IsFavorite;
            Title = LinkKindDetector.SuggestTitle(item.FullPath, item.Kind);
            LastAccessedText = FormatAge(item.LastAccessed, now);

            OpenCommand = root.CreateCommand(_ => root.OpenRecentAsync(this));
            PinCommand = root.CreateCommand(_ => root.PinRecentAsync(this));
        }

        public string Title { get; }

        public string FullPath { get; }

        public LinkKind Kind { get; }

        public bool IsFavorite { get; }

        public string LastAccessedText { get; }

        public bool IsVisible
        {
            get => _isVisible;
            internal set => SetProperty(ref _isVisible, value);
        }

        public ICommand OpenCommand { get; }

        /// <summary>Copies the item into one of the user's sections.</summary>
        public ICommand PinCommand { get; }

        internal LinkItem ToLinkItem() => new LinkItem { Title = Title, Target = FullPath, Kind = Kind };

        internal static string FormatAge(DateTimeOffset? lastAccessed, DateTimeOffset now)
        {
            if (!lastAccessed.HasValue)
            {
                return string.Empty;
            }

            var local = lastAccessed.Value.ToLocalTime();
            var today = now.ToLocalTime().Date;

            if (local.Date == today)
            {
                return "Today " + local.ToString("HH:mm", CultureInfo.CurrentCulture);
            }

            if (local.Date == today.AddDays(-1))
            {
                return "Yesterday";
            }

            if (local.Date > today.AddDays(-7))
            {
                return local.ToString("dddd", CultureInfo.CurrentCulture);
            }

            return local.ToString("d MMM yyyy", CultureInfo.CurrentCulture);
        }
    }
}

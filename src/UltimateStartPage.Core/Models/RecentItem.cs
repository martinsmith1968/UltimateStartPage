using System;

namespace UltimateStartPage.Core.Models
{
    /// <summary>A solution, project or folder from Visual Studio's own most-recently-used list.</summary>
    public sealed class RecentItem
    {
        public RecentItem(string fullPath, LinkKind kind, DateTimeOffset? lastAccessed, bool isFavorite)
        {
            FullPath = fullPath;
            Kind = kind;
            LastAccessed = lastAccessed;
            IsFavorite = isFavorite;
        }

        public string FullPath { get; }

        public LinkKind Kind { get; }

        public DateTimeOffset? LastAccessed { get; }

        public bool IsFavorite { get; }
    }
}

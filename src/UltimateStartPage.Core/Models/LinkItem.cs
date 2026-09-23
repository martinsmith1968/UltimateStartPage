using System;

namespace UltimateStartPage.Core.Models
{
    /// <summary>A single entry in a section: a solution, project, folder, file or web link.</summary>
    public sealed class LinkItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Title { get; set; } = string.Empty;

        /// <summary>Full path on disk, or an absolute http(s) URL.</summary>
        public string Target { get; set; } = string.Empty;

        public LinkKind Kind { get; set; }

        public string? Description { get; set; }
    }
}

using System;
using System.Collections.Generic;

namespace UltimateStartPage.Core.Models
{
    /// <summary>A user-defined, titled group of links shown as one card on the start page.</summary>
    public sealed class Section
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Title { get; set; } = string.Empty;

        public bool IsCollapsed { get; set; }

        public List<LinkItem> Links { get; set; } = new List<LinkItem>();
    }
}

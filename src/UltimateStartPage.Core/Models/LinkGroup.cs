using System;
using System.Collections.Generic;

namespace UltimateStartPage.Core.Models
{
    /// <summary>
    /// A named group of solution/project links shown together on the start page.
    /// </summary>
    public class LinkGroup
    {
        public string? Name { get; set; }

        public IList<SolutionLink> Links { get; set; }

        public LinkGroup()
        {
            Links = new List<SolutionLink>();
        }

        public LinkGroup(string name) : this()
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }
}

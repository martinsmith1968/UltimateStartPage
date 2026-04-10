using System;

namespace UltimateStartPage.Core.Models
{
    /// <summary>
    /// Represents a user-defined link to a .sln or .csproj file.
    /// </summary>
    public class SolutionLink
    {
        public string? Name { get; set; }

        public string? FilePath { get; set; }

        public DateTime LastOpened { get; set; }

        public SolutionLink() { }

        public SolutionLink(string name, string filePath)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }
    }
}

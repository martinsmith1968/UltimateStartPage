using System.Collections.Generic;

namespace UltimateStartPage.Core.Models
{
    /// <summary>The persisted start page: an ordered list of sections. Serialised to layout.json.</summary>
    public sealed class StartPageLayout
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public List<Section> Sections { get; set; } = new List<Section>();
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using UltimateStartPage.Core.Models;

namespace UltimateStartPage.Core.Services
{
    /// <summary>Loads and saves the user's start page layout.</summary>
    public interface ILayoutStore
    {
        /// <summary>
        /// Raised, on a background thread, when the layout file is changed by someone other than this store: another
        /// Visual Studio instance, a sync client or a hand edit. Handlers should reload.
        /// </summary>
        event EventHandler? ExternalChange;

        /// <summary>Full path of the layout file currently in use.</summary>
        string FilePath { get; }

        /// <summary>Loads the layout, falling back to a default layout if the file is missing or unreadable.</summary>
        Task<StartPageLayout> LoadAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Saves the layout. Throws <see cref="LayoutConflictException"/> instead of writing if the file has changed
        /// since it was last loaded or saved here, so changes made elsewhere are never silently overwritten.
        /// </summary>
        Task SaveAsync(StartPageLayout layout, CancellationToken cancellationToken);

        /// <summary>True if the file now differs from what this store last loaded or saved.</summary>
        Task<bool> HasExternalChangesAsync(CancellationToken cancellationToken);

        /// <summary>Reads a layout from an arbitrary file. Throws <see cref="LayoutFormatException"/> if it is not a valid layout.</summary>
        Task<StartPageLayout> ImportAsync(string path, CancellationToken cancellationToken);

        Task ExportAsync(StartPageLayout layout, string path, CancellationToken cancellationToken);
    }
}

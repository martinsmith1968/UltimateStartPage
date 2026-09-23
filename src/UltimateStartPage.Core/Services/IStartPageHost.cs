using System.Threading;
using System.Threading.Tasks;
using UltimateStartPage.Core.Models;

namespace UltimateStartPage.Core.Services
{
    /// <summary>Opens links inside the IDE. Implemented against the Visual Studio shell in the extension.</summary>
    public interface ILinkLauncher
    {
        /// <summary>True if the link's target is reachable (always true for URLs).</summary>
        bool TargetExists(LinkItem link);

        /// <summary>Kind for a new target. Unlike <see cref="LinkKindDetector"/> this may check the disk, so a folder named "My.Repo" is still a folder.</summary>
        LinkKind ResolveKind(string target);

        Task OpenAsync(LinkItem link, CancellationToken cancellationToken);

        /// <summary>Opens Explorer with the target selected.</summary>
        Task OpenContainingFolderAsync(LinkItem link, CancellationToken cancellationToken);
    }

    /// <summary>The standard "get started" actions from Visual Studio's own start window.</summary>
    public interface IShellActions
    {
        Task OpenProjectOrSolutionAsync(CancellationToken cancellationToken);

        Task OpenFolderAsync(CancellationToken cancellationToken);

        Task CloneRepositoryAsync(CancellationToken cancellationToken);

        Task NewProjectAsync(CancellationToken cancellationToken);

        Task OpenFileInEditorAsync(string path, CancellationToken cancellationToken);

        /// <summary>Opens this extension's page in Tools &gt; Options.</summary>
        Task OpenOptionsAsync(CancellationToken cancellationToken);
    }

    /// <summary>User-facing settings, backed by Tools &gt; Options in the extension.</summary>
    public interface IStartPageSettings
    {
        bool ShowRecentItems { get; }

        int MaxRecentItems { get; }

        bool ShowGetStartedActions { get; }
    }
}

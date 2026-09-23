using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using UltimateStartPage.Core.Models;
using UltimateStartPage.Core.Services;
using IAsyncServiceProvider = Microsoft.VisualStudio.Shell.IAsyncServiceProvider;

namespace UltimateStartPage.Services
{
    /// <summary>Opens start page links using the Visual Studio shell.</summary>
    internal sealed class VsLinkLauncher : ILinkLauncher
    {
        private readonly IAsyncServiceProvider _services;
        private readonly JoinableTaskFactory _joinableTaskFactory;
        private readonly ILogger<VsLinkLauncher> _logger;

        public VsLinkLauncher(IAsyncServiceProvider services, JoinableTaskFactory joinableTaskFactory, ILogger<VsLinkLauncher> logger)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _joinableTaskFactory = joinableTaskFactory ?? throw new ArgumentNullException(nameof(joinableTaskFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool TargetExists(LinkItem link)
        {
            if (link.Kind == LinkKind.Url)
            {
                return true;
            }

            return File.Exists(link.Target) || Directory.Exists(link.Target);
        }

        public LinkKind ResolveKind(string target)
        {
            if (LinkKindDetector.IsWebUrl(target))
            {
                return LinkKind.Url;
            }

            return Directory.Exists(target) ? LinkKind.Folder : LinkKindDetector.Detect(target);
        }

        public async Task OpenAsync(LinkItem link, CancellationToken cancellationToken)
        {
            // A link saved as a file may since have become a folder (or was mis-detected); trust the disk. That check can
            // be slow on a network share, and this is called from the UI thread, so it runs on the thread pool.
            var kind = link.Kind != LinkKind.Url && await Task.Run(() => Directory.Exists(link.Target), cancellationToken)
                ? LinkKind.Folder
                : link.Kind;

            await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            switch (kind)
            {
                case LinkKind.Solution:
                case LinkKind.Project:
                    await OpenSolutionOrProjectAsync(link.Target);
                    break;

                case LinkKind.Folder:
                    await OpenFolderAsync(link.Target);
                    break;

                case LinkKind.File:
                    VsShellUtilities.OpenDocument(ServiceProvider.GlobalProvider, link.Target);
                    break;

                case LinkKind.Url:
                    Process.Start(new ProcessStartInfo(link.Target) { UseShellExecute = true })?.Dispose();
                    break;

                default:
                    throw new NotSupportedException($"Unknown link kind '{link.Kind}'.");
            }
        }

        public Task OpenContainingFolderAsync(LinkItem link, CancellationToken cancellationToken)
        {
            if (link.Kind == LinkKind.Url)
            {
                return Task.CompletedTask;
            }

            // /select opens the parent folder with the item highlighted; works for files and folders.
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{link.Target}\"") { UseShellExecute = true })?.Dispose();
            return Task.CompletedTask;
        }

        private async Task OpenSolutionOrProjectAsync(string path)
        {
            var solution = await GetRequiredServiceAsync<SVsSolution, IVsSolution>();

            // Opening a project file directly makes VS create a temporary solution around it, as File > Open does.
            // If a solution is already open, VS handles the save-changes prompt and closes it first.
            _logger.LogDebug("IVsSolution.OpenSolutionFile({Path})", path);
            ErrorHandler.ThrowOnFailure(solution.OpenSolutionFile(0, path));
        }

        private async Task OpenFolderAsync(string path)
        {
            var solution = await GetRequiredServiceAsync<SVsSolution, IVsSolution>();

            if (solution is IVsSolution7 folderAware)
            {
                _logger.LogDebug("IVsSolution7.OpenFolder({Path})", path);
                folderAware.OpenFolder(path);
                return;
            }

            // Fallback for shells without IVsSolution7: the same command File > Open > Folder runs.
            var dte = await GetRequiredServiceAsync<EnvDTE.DTE, EnvDTE.DTE>();
            dte.ExecuteCommand("File.OpenFolder", $"\"{path}\"");
        }

        private async Task<TInterface> GetRequiredServiceAsync<TService, TInterface>()
            where TInterface : class
        {
            var service = await _services.GetServiceAsync(typeof(TService)) as TInterface;
            return service ?? throw new InvalidOperationException($"The {typeof(TService).Name} service is not available.");
        }
    }
}

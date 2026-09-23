using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using UltimateStartPage.Core.Services;
using UltimateStartPage.Options;
using IAsyncServiceProvider = Microsoft.VisualStudio.Shell.IAsyncServiceProvider;

namespace UltimateStartPage.Services
{
    /// <summary>Runs the IDE's own "get started" commands by their canonical names.</summary>
    internal sealed class VsShellActions : IShellActions
    {
        // Canonical names have moved between VS versions; the first one that exists wins.
        private static readonly string[] CloneCommands = { "Git.CloneRepository", "Git.Clone", "File.CloneRepository", "Team.Git.Clone" };

        private readonly IAsyncServiceProvider _services;
        private readonly JoinableTaskFactory _joinableTaskFactory;
        private readonly AsyncPackage _package;
        private readonly ILogger<VsShellActions> _logger;

        public VsShellActions(IAsyncServiceProvider services, JoinableTaskFactory joinableTaskFactory, AsyncPackage package, ILogger<VsShellActions> logger)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _joinableTaskFactory = joinableTaskFactory ?? throw new ArgumentNullException(nameof(joinableTaskFactory));
            _package = package ?? throw new ArgumentNullException(nameof(package));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task OpenProjectOrSolutionAsync(CancellationToken cancellationToken) =>
            ExecuteFirstAvailableAsync(cancellationToken, "File.OpenProject");

        public Task OpenFolderAsync(CancellationToken cancellationToken) =>
            ExecuteFirstAvailableAsync(cancellationToken, "File.OpenFolder");

        public Task CloneRepositoryAsync(CancellationToken cancellationToken) =>
            ExecuteFirstAvailableAsync(cancellationToken, CloneCommands);

        public Task NewProjectAsync(CancellationToken cancellationToken) =>
            ExecuteFirstAvailableAsync(cancellationToken, "File.NewProject");

        public async Task OpenFileInEditorAsync(string path, CancellationToken cancellationToken)
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            VsShellUtilities.OpenDocument(_package, path);
        }

        public async Task OpenOptionsAsync(CancellationToken cancellationToken)
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            _package.ShowOptionPage(typeof(OptionsProvider.GeneralOptionsPage));
        }

        private async Task ExecuteFirstAvailableAsync(CancellationToken cancellationToken, params string[] commandNames)
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            if (!(await _services.GetServiceAsync(typeof(EnvDTE.DTE)) is EnvDTE.DTE dte))
            {
                throw new InvalidOperationException("The DTE service is not available.");
            }

            foreach (var name in commandNames)
            {
                try
                {
                    dte.ExecuteCommand(name);
                    _logger.LogDebug("Executed {CommandName}", name);
                    return;
                }
                catch (COMException ex)
                {
                    _logger.LogDebug(ex, "Command {CommandName} is not available", name);
                }
                catch (ArgumentException ex)
                {
                    _logger.LogDebug(ex, "Command {CommandName} is not available", name);
                }
            }

            throw new InvalidOperationException(
                $"None of these Visual Studio commands are available: {string.Join(", ", commandNames)}.");
        }
    }
}

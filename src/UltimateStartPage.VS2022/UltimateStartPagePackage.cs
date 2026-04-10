using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using UltimateStartPage.Core.Services;
using UltimateStartPage.VS2022.ToolWindows;
using Task = System.Threading.Tasks.Task;

namespace UltimateStartPage.VS2022
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuids.PackageGuidString)]
    [ProvideToolWindow(
        typeof(StartPageToolWindow),
        Style = VsDockStyle.Tabbed,
        Window = EnvDTE.Constants.vsWindowKindMainWindow,
        MultiInstances = false,
        Transient = false)]
    // Show the tool window automatically whenever there is no solution open.
    // BackgroundLoad is required because we inherit from AsyncPackage.
    [ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class UltimateStartPagePackage : AsyncPackage
    {
        private LinkRepository _linkRepository;

        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            // Initialize LinkRepository singleton for this package instance
            _linkRepository = new LinkRepository();

            // Register as a service so tool windows can retrieve it
            AddService(typeof(ILinkRepository), async (container, ct, serviceType) =>
            {
                await Task.CompletedTask;
                return _linkRepository;
            });

            await ShowStartPageAsync();
        }

        internal ILinkRepository GetLinkRepository()
        {
            return _linkRepository;
        }

        private async Task ShowStartPageAsync()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();

            var window = await FindToolWindowAsync(
                typeof(StartPageToolWindow),
                id: 0,
                create: true,
                cancellationToken: DisposalToken);

            if (window?.Frame is IVsWindowFrame windowFrame)
                ErrorHandler.ThrowOnFailure(windowFrame.Show());
        }
    }
}

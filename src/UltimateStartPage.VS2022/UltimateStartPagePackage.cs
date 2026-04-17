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
    // Menus.ctmenu is compiled from UltimateStartPage.VS2022Commands.vsct and embedded as a resource.
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(
        typeof(StartPageToolWindow),
        Style = VsDockStyle.Tabbed,
        Window = EnvDTE.Constants.vsWindowKindMainWindow,
        MultiInstances = false,
        Transient = false)]
    // Load when VS shell is fully initialised so the View menu command is always registered.
    [ProvideAutoLoad(VSConstants.UICONTEXT.ShellInitialized_string, PackageAutoLoadFlags.BackgroundLoad)]
    // Also auto-show the window explicitly when there is no solution open.
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

            // Register the View > Ultimate Start Page command
            await ShowStartPageCommand.InitializeAsync(this);

            // Auto-show the start page only when no solution is currently open
            await ShowStartPageAsync();
        }

        internal ILinkRepository GetLinkRepository()
        {
            return _linkRepository;
        }

        private async Task ShowStartPageAsync()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();

            // Don't auto-show if a solution is already open — the user is working on something
            var solution = await GetServiceAsync(typeof(SVsSolution)) as IVsSolution;
            object isOpenObj = null;
            solution?.GetProperty((int)__VSPROPID.VSPROPID_IsSolutionOpen, out isOpenObj);
            if (isOpenObj is bool isOpen && isOpen)
                return;

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

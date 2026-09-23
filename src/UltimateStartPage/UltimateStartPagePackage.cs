using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Community.VisualStudio.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using UltimateStartPage.Core.Services;
using UltimateStartPage.Core.ViewModels;
using UltimateStartPage.Logging;
using UltimateStartPage.Options;
using UltimateStartPage.Services;
using UltimateStartPage.ToolWindows;
using IAsyncServiceProvider = Microsoft.VisualStudio.Shell.IAsyncServiceProvider;
using Task = System.Threading.Tasks.Task;

namespace UltimateStartPage
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(StartPageWindow.Pane), Style = VsDockStyle.MDI, MultiInstances = false)]
    [ProvideOptionPage(typeof(OptionsProvider.GeneralOptionsPage), Vsix.Name, "General", 0, 0, true, SupportsProfiles = true)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.ShellInitialized_string, PackageAutoLoadFlags.BackgroundLoad)]
    [Guid(PackageGuids.PackageString)]
    public sealed class UltimateStartPagePackage : ToolkitPackage
    {
        private static readonly TimeSpan ShutdownSaveTimeout = TimeSpan.FromSeconds(5);

        private Microsoft.Extensions.DependencyInjection.ServiceProvider? _appServices;
        private ILogger<UltimateStartPagePackage>? _logger;
        private EnvDTE.DTEEvents? _dteEvents; // must be held, or the COM event sink is collected
        private volatile bool _isShuttingDown;
        private volatile bool _verboseLogging;

        /// <summary>The loaded package, for tool windows that are created by the shell rather than by us.</summary>
        internal static UltimateStartPagePackage? Instance { get; private set; }

        /// <summary>The extension's own DI container (Microsoft.Extensions.DependencyInjection).</summary>
        internal IServiceProvider AppServices =>
            _appServices ?? throw new InvalidOperationException("The package has not finished initialising.");

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            // Load options asynchronously. GeneralOptions.Instance does a synchronous JoinableTaskFactory.Run, which must
            // not happen during package load or from the thread pool (loggers run there).
            var options = await GeneralOptions.GetLiveInstanceAsync();
            _verboseLogging = options.VerboseLogging;

            var outputPane = await CreateOutputPaneAsync();
            var localAppDataDir = await GetLocalAppDataDirAsync();

            _appServices = ConfigureServices(outputPane, localAppDataDir, options).BuildServiceProvider(validateScopes: true);
            _logger = AppServices.GetRequiredService<ILogger<UltimateStartPagePackage>>();
            _logger.LogInformation("{ExtensionName} {Version} loaded", Vsix.Name, Vsix.Version);
            Instance = this;

            await this.RegisterCommandsAsync();
            this.RegisterToolWindows();

            VS.Events.SolutionEvents.OnAfterOpenSolution += OnAfterOpenSolution;
            VS.Events.SolutionEvents.OnAfterCloseSolution += OnAfterCloseSolution;
            GeneralOptions.Saved += OnOptionsSaved;

            if (await GetServiceAsync(typeof(EnvDTE.DTE)) is EnvDTE.DTE dte)
            {
                _dteEvents = dte.Events.DTEEvents;
                _dteEvents.OnBeginShutdown += OnBeginShutdown;
            }

            // Never await a tool window from InitializeAsync: creating the window waits for this package to finish
            // loading, and this method would be waiting for the window, so Visual Studio hangs at startup.
            if (options.ShowOnStartup)
            {
                RunInBackground("Show start page on startup", async () =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(250), DisposalToken);
                    if (!await IsSolutionOpenAsync())
                    {
                        await StartPageWindow.ShowAsync();
                    }
                });
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                GeneralOptions.Saved -= OnOptionsSaved;
                VS.Events.SolutionEvents.OnAfterOpenSolution -= OnAfterOpenSolution;
                VS.Events.SolutionEvents.OnAfterCloseSolution -= OnAfterCloseSolution;
                Instance = null;
                _appServices?.Dispose();
                _appServices = null;
            }

            base.Dispose(disposing);
        }

        private IServiceCollection ConfigureServices(IVsOutputWindowPane outputPane, string localAppDataDir, GeneralOptions options)
        {
            var services = new ServiceCollection();

            services.AddLogging(builder => builder
                .SetMinimumLevel(LogLevel.Trace)
                .AddProvider(new OutputPaneLoggerProvider(
                    outputPane,
                    () => _verboseLogging ? LogLevel.Debug : LogLevel.Information)));

            // Visual Studio plumbing
            services.AddSingleton<AsyncPackage>(this);
            services.AddSingleton<IAsyncServiceProvider>(this);
            services.AddSingleton(JoinableTaskFactory);

            // Core services
            services.AddSingleton(new LayoutStoreOptions(ResolveLayoutFilePath(options)));
            services.AddSingleton<ILayoutStore, JsonLayoutStore>();
            services.AddSingleton(new VsRecentItemsOptions(Path.Combine(localAppDataDir, "ApplicationPrivateSettings.xml")));
            services.AddSingleton<IRecentItemsSource, VsRecentItemsReader>();
            services.AddSingleton<IStartPageSettings, VsStartPageSettings>();

            // Shell integration
            services.AddSingleton<ILinkLauncher, VsLinkLauncher>();
            services.AddSingleton<IShellActions, VsShellActions>();
            services.AddSingleton<IDialogService, WpfDialogService>();

            // One start page per IDE instance, so the view model outlives the window being closed and reopened.
            services.AddSingleton<StartPageViewModel>();

            return services;
        }

        private static string ResolveLayoutFilePath(GeneralOptions options)
        {
            var configured = options.LayoutFilePath;
            return string.IsNullOrWhiteSpace(configured)
                ? LayoutStoreOptions.DefaultFilePath
                : Path.GetFullPath(Environment.ExpandEnvironmentVariables(configured.Trim().Trim('"')));
        }

        private async Task<IVsOutputWindowPane> CreateOutputPaneAsync()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();

            var outputWindow = (IVsOutputWindow)await GetServiceAsync(typeof(SVsOutputWindow));
            var paneGuid = PackageGuids.OutputPane;
            ErrorHandler.ThrowOnFailure(outputWindow.CreatePane(ref paneGuid, Vsix.Name, 1, 0));
            ErrorHandler.ThrowOnFailure(outputWindow.GetPane(ref paneGuid, out var pane));
            return pane;
        }

        private async Task<string> GetLocalAppDataDirAsync()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();

            // e.g. %LOCALAPPDATA%\Microsoft\VisualStudio\18.0_1a2b3c4d — honours /rootsuffix Exp when debugging.
            var shell = (IVsShell)await GetServiceAsync(typeof(SVsShell));
            ErrorHandler.ThrowOnFailure(shell.GetProperty((int)__VSSPROPID4.VSSPROPID_LocalAppDataDir, out var value));
            return (string)value;
        }

        private async Task<bool> IsSolutionOpenAsync()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();

            if (!(await GetServiceAsync(typeof(SVsSolution)) is IVsSolution solution))
            {
                return false;
            }

            ErrorHandler.ThrowOnFailure(solution.GetProperty((int)__VSPROPID.VSPROPID_IsSolutionOpen, out var isOpen));
            return isOpen is bool open && open;
        }

        private void OnAfterOpenSolution(Community.VisualStudio.Toolkit.Solution? solution)
        {
            if (!GeneralOptions.Instance.CloseWhenSolutionOpens)
            {
                return;
            }

            RunInBackground(nameof(OnAfterOpenSolution), async () =>
            {
                await JoinableTaskFactory.SwitchToMainThreadAsync(DisposalToken);

                var uiShell = (IVsUIShell)await GetServiceAsync(typeof(SVsUIShell));
                var windowGuid = typeof(StartPageWindow.Pane).GUID;

                // FTW_fFrameOnly: only find an existing frame, never create one just to close it.
                if (ErrorHandler.Succeeded(uiShell.FindToolWindow((uint)__VSFINDTOOLWIN.FTW_fFrameOnly, ref windowGuid, out var frame))
                    && frame != null)
                {
                    frame.CloseFrame((uint)__FRAMECLOSE.FRAMECLOSE_NoSave);
                }
            });
        }

        private void OnAfterCloseSolution()
        {
            if (_isShuttingDown || !GeneralOptions.Instance.ShowWhenSolutionCloses)
            {
                return;
            }

            RunInBackground(nameof(OnAfterCloseSolution), async () =>
            {
                // Switching solutions closes one and immediately opens another; don't flash the page in between.
                await Task.Delay(TimeSpan.FromMilliseconds(500), DisposalToken);
                if (_isShuttingDown || await IsSolutionOpenAsync())
                {
                    return;
                }

                await StartPageWindow.ShowAsync();
                await AppServices.GetRequiredService<StartPageViewModel>().RefreshRecentItemsAsync(DisposalToken);
            });
        }

        /// <summary>
        /// Edits are saved after a short delay, so one made just before closing Visual Studio may not be on disk yet.
        /// VS doesn't wait for background work at shutdown, so block here (on the UI thread) until it is.
        /// </summary>
        private void OnBeginShutdown()
        {
            _isShuttingDown = true;

            var viewModel = _appServices?.GetService<StartPageViewModel>();
            if (viewModel == null || !viewModel.HasUnsavedChanges)
            {
                return;
            }

            try
            {
                // A synchronous event can't be awaited. JoinableTaskFactory.Run is the deadlock-safe way to block the
                // UI thread on async work. The timeout stops an unreachable network share from hanging shutdown.
#pragma warning disable VSTHRD102 // Implement internal logic asynchronously
                JoinableTaskFactory.Run(async () =>
                {
                    using (var timeout = new CancellationTokenSource(ShutdownSaveTimeout))
                    {
                        await viewModel.SaveUnsavedChangesAsync(timeout.Token);
                    }
                });
#pragma warning restore VSTHRD102
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning(
                    "Gave up saving the start page after {Timeout}; the latest change may be lost", ShutdownSaveTimeout);
            }
            catch (Exception ex)
            {
                // Never block shutdown on this.
                _logger?.LogError(ex, "Saving the start page on shutdown failed");
            }
        }

        private void OnOptionsSaved(GeneralOptions options)
        {
            _verboseLogging = options.VerboseLogging;

            RunInBackground(nameof(OnOptionsSaved), async () =>
            {
                await JoinableTaskFactory.SwitchToMainThreadAsync(DisposalToken);
                await AppServices.GetRequiredService<StartPageViewModel>().RefreshRecentItemsAsync(DisposalToken);
            });
        }

        /// <summary>Starts work from a synchronous event handler without async void, logging any failure.</summary>
        private void RunInBackground(string operation, Func<Task> work)
        {
            _ = JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    await work();
                }
                catch (OperationCanceledException)
                {
                    // VS is shutting down.
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "{Operation} failed", operation);
                }
            });
        }
    }
}

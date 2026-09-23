using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Community.VisualStudio.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using UltimateStartPage.Core.ViewModels;

namespace UltimateStartPage.ToolWindows
{
    /// <summary>The start page itself: a single-instance tool window that docks in the document well.</summary>
    public class StartPageWindow : BaseToolWindow<StartPageWindow>
    {
        public override string GetTitle(int toolWindowId) => "Start Page";

        public override Type PaneType => typeof(Pane);

        public override async Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
        {
            var package = UltimateStartPagePackage.Instance
                ?? throw new InvalidOperationException("Ultimate Start Page has not finished loading.");
            var viewModel = package.AppServices.GetRequiredService<StartPageViewModel>();

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var control = new StartPageControl(viewModel);
            await viewModel.InitializeAsync(cancellationToken);
            return control;
        }

        [Guid(PackageGuids.ToolWindowString)]
        internal class Pane : ToolkitToolWindowPane
        {
            public Pane()
            {
                BitmapImageMoniker = KnownMonikers.Home;
            }
        }
    }
}

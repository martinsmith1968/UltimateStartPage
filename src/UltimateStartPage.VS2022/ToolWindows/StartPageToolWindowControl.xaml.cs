using System;
using System.Windows.Controls;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using UltimateStartPage.Core.Services;
using UltimateStartPage.Core.ViewModels;

namespace UltimateStartPage.VS2022.ToolWindows
{
    public partial class StartPageToolWindowControl : UserControl
    {
        public StartPageToolWindowControl(ILinkRepository repository)
        {
            if (repository == null)
                throw new ArgumentNullException(nameof(repository));

            InitializeComponent();

            var viewModel = new StartPageViewModel(repository, OpenSolutionInVS);
            DataContext = viewModel;

            // Fire-and-forget load on UI thread after InitializeComponent.
            _ = viewModel.LoadAsync();
        }

        private void OpenSolutionInVS(string path)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dte = Package.GetGlobalService(typeof(DTE)) as DTE;
            if (dte == null)
                return; // Gracefully handle null DTE

            try
            {
                dte.Solution.Open(path);
            }
            catch (Exception)
            {
                // Silently handle errors - future logging framework will capture this
            }
        }
    }
}


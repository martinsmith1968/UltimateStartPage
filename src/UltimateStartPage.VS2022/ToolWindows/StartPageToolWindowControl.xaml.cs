using System;
using System.Windows.Controls;
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

            var viewModel = new StartPageViewModel(repository);
            DataContext = viewModel;

            // Fire-and-forget load on UI thread after InitializeComponent.
            _ = viewModel.LoadAsync();
        }
    }
}


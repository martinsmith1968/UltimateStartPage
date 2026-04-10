using System.Windows.Controls;
using UltimateStartPage.Core.Services;
using UltimateStartPage.Core.ViewModels;

namespace UltimateStartPage.VS2022.ToolWindows
{
    public partial class StartPageToolWindowControl : UserControl
    {
        public StartPageToolWindowControl()
        {
            InitializeComponent();

            // TODO (McManus): replace with proper DI via AsyncPackage.GetServiceAsync<ILinkRepository>()
            // once MEF/service-provider wiring is implemented in UltimateStartPagePackage.
            // For now, construct directly — LinkRepository defaults to %APPDATA%\UltimateStartPage\links.json.
            var repository = new LinkRepository();
            var viewModel = new StartPageViewModel(repository);
            DataContext = viewModel;

            // Fire-and-forget load on UI thread after InitializeComponent.
            _ = viewModel.LoadAsync();
        }
    }
}


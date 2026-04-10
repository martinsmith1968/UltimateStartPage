using System.Windows.Controls;
using UltimateStartPage.VS2022.ViewModels;

namespace UltimateStartPage.VS2022.ToolWindows
{
    public partial class StartPageToolWindowControl : UserControl
    {
        public StartPageToolWindowControl()
        {
            InitializeComponent();

            // Stub DataContext — McManus will replace with DI-resolved StartPageViewModel
            // once ILinkRepository is injected via the package service provider.
            DataContext = new StartPageViewModel();
        }
    }
}


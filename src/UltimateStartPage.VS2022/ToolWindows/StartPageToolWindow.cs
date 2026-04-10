using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace UltimateStartPage.VS2022.ToolWindows
{
    [Guid("4C3D2E1F-A0B9-4876-C5D4-E3F2A1B09876")]
    public class StartPageToolWindow : ToolWindowPane
    {
        public StartPageToolWindow() : base(null)
        {
            Caption = "Ultimate Start Page";
            // Content is set here so VS can create the pane without the package being fully loaded.
            // Verbal owns the XAML; this is a placeholder until the real control is designed.
            Content = new StartPageToolWindowControl();
        }
    }
}

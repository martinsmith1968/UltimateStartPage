using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using UltimateStartPage.Core.Services;

namespace UltimateStartPage.VS2022.ToolWindows
{
    [Guid("4C3D2E1F-A0B9-4876-C5D4-E3F2A1B09876")]
    public class StartPageToolWindow : ToolWindowPane
    {
        public StartPageToolWindow() : base(null)
        {
            Caption = "Ultimate Start Page";
            // Content will be created lazily when the ILinkRepository is available
        }

        protected override void Initialize()
        {
            base.Initialize();

            // Get ILinkRepository from the package
            var package = Package as UltimateStartPagePackage;
            var repository = package?.GetLinkRepository();

            if (repository != null)
            {
                Content = new StartPageToolWindowControl(repository);
            }
        }
    }
}

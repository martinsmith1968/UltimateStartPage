using System.ComponentModel;
using System.Runtime.InteropServices;
using Community.VisualStudio.Toolkit;

namespace UltimateStartPage.Options
{
    internal partial class OptionsProvider
    {
        /// <summary>Tools &gt; Options &gt; Ultimate Start Page &gt; General.</summary>
        [ComVisible(true)]
        public class GeneralOptionsPage : BaseOptionPage<GeneralOptions>
        {
        }
    }

    public class GeneralOptions : BaseOptionModel<GeneralOptions>
    {
        [Category("Startup")]
        [DisplayName("Show on startup")]
        [Description("Open the start page when Visual Studio starts without a solution. For the best experience also set Tools > Options > Environment > Startup > 'On startup, open' to 'Empty environment'.")]
        [DefaultValue(true)]
        public bool ShowOnStartup { get; set; } = true;

        [Category("Startup")]
        [DisplayName("Close when a solution opens")]
        [Description("Close the start page tab once a solution, project or folder has been opened.")]
        [DefaultValue(true)]
        public bool CloseWhenSolutionOpens { get; set; } = true;

        [Category("Startup")]
        [DisplayName("Show when a solution closes")]
        [Description("Bring the start page back after closing a solution.")]
        [DefaultValue(true)]
        public bool ShowWhenSolutionCloses { get; set; } = true;

        [Category("Content")]
        [DisplayName("Show recent items")]
        [Description("Show Visual Studio's own recent solutions and folders beside your sections.")]
        [DefaultValue(true)]
        public bool ShowRecentItems { get; set; } = true;

        [Category("Content")]
        [DisplayName("Maximum recent items")]
        [Description("How many recent items to list (1-100).")]
        [DefaultValue(15)]
        public int MaxRecentItems { get; set; } = 15;

        [Category("Content")]
        [DisplayName("Show 'Get started' actions")]
        [Description("Show Open project, Open folder, Clone repository and New project shortcuts.")]
        [DefaultValue(true)]
        public bool ShowGetStartedActions { get; set; } = true;

        [Category("Storage")]
        [DisplayName("Layout file")]
        [Description("Where your sections are stored. Leave empty for %APPDATA%\\UltimateStartPage\\layout.json. Point it at a synced or shared folder to use the same layout on several machines. Environment variables are expanded. Takes effect after restarting Visual Studio.")]
        [DefaultValue("")]
        public string LayoutFilePath { get; set; } = string.Empty;

        [Category("Diagnostics")]
        [DisplayName("Verbose logging")]
        [Description("Write debug-level messages to the 'Ultimate Start Page' pane of the Output window.")]
        [DefaultValue(false)]
        public bool VerboseLogging { get; set; }
    }
}

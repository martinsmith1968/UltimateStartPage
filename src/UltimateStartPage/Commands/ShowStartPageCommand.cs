using System.Threading.Tasks;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using UltimateStartPage.ToolWindows;

namespace UltimateStartPage.Commands
{
    /// <summary>View &gt; Other Windows &gt; Ultimate Start Page, File &gt; Ultimate Start Page, Ctrl+Alt+Home.</summary>
    [Command(PackageGuids.CommandSetString, PackageIds.ShowStartPage)]
    internal sealed class ShowStartPageCommand : BaseCommand<ShowStartPageCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await StartPageWindow.ShowAsync();
        }
    }
}

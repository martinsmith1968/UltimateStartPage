using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using UltimateStartPage.VS2022.ToolWindows;
using Task = System.Threading.Tasks.Task;

namespace UltimateStartPage.VS2022
{
    /// <summary>
    /// Registers the "View > Ultimate Start Page" command and handles its execution.
    /// </summary>
    internal sealed class ShowStartPageCommand
    {
        /// <summary>Command ID — must match IDSymbol value in the .vsct file.</summary>
        public const int CommandId = 0x0100;

        /// <summary>Command set GUID — must match GuidSymbol in the .vsct file.</summary>
        public static readonly Guid CommandSet = new Guid(PackageGuids.CmdSetGuidString);

        private readonly AsyncPackage _package;

        private ShowStartPageCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
            if (commandService == null) throw new ArgumentNullException(nameof(commandService));

            commandService.AddCommand(new MenuCommand(Execute, new CommandID(CommandSet, CommandId)));
        }

        /// <summary>
        /// Creates and registers the command. Must be called from the UI thread.
        /// </summary>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            new ShowStartPageCommand(package, commandService);
        }

        private void Execute(object sender, EventArgs e)
        {
            _ = _package.JoinableTaskFactory.RunAsync(async () =>
            {
                var window = await _package.FindToolWindowAsync(
                    typeof(StartPageToolWindow),
                    id: 0,
                    create: true,
                    cancellationToken: _package.DisposalToken);

                if (window?.Frame is Microsoft.VisualStudio.Shell.Interop.IVsWindowFrame frame)
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(frame.Show());
                }
            });
        }
    }
}

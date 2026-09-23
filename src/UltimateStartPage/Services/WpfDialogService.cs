using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using UltimateStartPage.Core.Services;
using UltimateStartPage.Dialogs;

namespace UltimateStartPage.Services
{
    /// <summary>Themed VS dialogs for the view models. Everything here runs on the UI thread.</summary>
    internal sealed class WpfDialogService : IDialogService
    {
        private const string LayoutFileFilter = "Start page layout (*.json)|*.json|All files|*.*";

        private readonly JoinableTaskFactory _joinableTaskFactory;
        private readonly ILinkLauncher _launcher;

        public WpfDialogService(JoinableTaskFactory joinableTaskFactory, ILinkLauncher launcher)
        {
            _joinableTaskFactory = joinableTaskFactory ?? throw new ArgumentNullException(nameof(joinableTaskFactory));
            _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        }

        public async Task<LinkEditResult?> EditLinkAsync(LinkEditRequest request, CancellationToken cancellationToken)
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var dialog = new LinkEditorDialog(request, _launcher.ResolveKind);
            return dialog.ShowModal() == true ? dialog.Result : null;
        }

        public async Task<string?> PromptForTextAsync(string title, string prompt, string initialValue, CancellationToken cancellationToken)
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var dialog = new TextPromptDialog(title, prompt, initialValue);
            return dialog.ShowModal() == true ? dialog.Value : null;
        }

        public async Task<bool> ConfirmAsync(string title, string message, CancellationToken cancellationToken)
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var result = VsShellUtilities.ShowMessageBox(
                ServiceProvider.GlobalProvider,
                message,
                title,
                OLEMSGICON.OLEMSGICON_QUERY,
                OLEMSGBUTTON.OLEMSGBUTTON_YESNO,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

            return result == (int)VSConstants.MessageBoxResult.IDYES;
        }

        public async Task ShowErrorAsync(string title, string message, CancellationToken cancellationToken)
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            VsShellUtilities.ShowMessageBox(
                ServiceProvider.GlobalProvider,
                message,
                title,
                OLEMSGICON.OLEMSGICON_WARNING,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }

        public async Task<string?> PickLayoutFileToImportAsync(CancellationToken cancellationToken)
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import start page layout",
                Filter = LayoutFileFilter,
                CheckFileExists = true,
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public async Task<string?> PickLayoutFileToExportAsync(CancellationToken cancellationToken)
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export start page layout",
                Filter = LayoutFileFilter,
                FileName = "start-page-layout.json",
                OverwritePrompt = true,
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
    }
}

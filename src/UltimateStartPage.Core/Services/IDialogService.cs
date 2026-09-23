using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UltimateStartPage.Core.Models;

namespace UltimateStartPage.Core.Services
{
    /// <summary>What the link editor dialog is opened with.</summary>
    public sealed class LinkEditRequest
    {
        public LinkEditRequest(string dialogTitle, IReadOnlyList<string> sectionTitles, int selectedSectionIndex)
        {
            DialogTitle = dialogTitle;
            SectionTitles = sectionTitles;
            SelectedSectionIndex = selectedSectionIndex;
        }

        public string DialogTitle { get; }

        public string Title { get; set; } = string.Empty;

        public string Target { get; set; } = string.Empty;

        public string? Description { get; set; }

        /// <summary>Sections the link can be placed in; the dialog lets the user move a link between them.</summary>
        public IReadOnlyList<string> SectionTitles { get; }

        public int SelectedSectionIndex { get; }
    }

    public sealed class LinkEditResult
    {
        public LinkEditResult(string title, string target, LinkKind kind, string? description, int sectionIndex)
        {
            Title = title;
            Target = target;
            Kind = kind;
            Description = description;
            SectionIndex = sectionIndex;
        }

        public string Title { get; }

        public string Target { get; }

        public LinkKind Kind { get; }

        public string? Description { get; }

        public int SectionIndex { get; }
    }

    /// <summary>Modal prompts, kept behind an interface so view models stay testable.</summary>
    public interface IDialogService
    {
        /// <returns>The edited link, or null if the user cancelled.</returns>
        Task<LinkEditResult?> EditLinkAsync(LinkEditRequest request, CancellationToken cancellationToken);

        /// <returns>The entered text, or null if the user cancelled.</returns>
        Task<string?> PromptForTextAsync(string title, string prompt, string initialValue, CancellationToken cancellationToken);

        Task<bool> ConfirmAsync(string title, string message, CancellationToken cancellationToken);

        Task ShowErrorAsync(string title, string message, CancellationToken cancellationToken);

        Task<string?> PickLayoutFileToImportAsync(CancellationToken cancellationToken);

        Task<string?> PickLayoutFileToExportAsync(CancellationToken cancellationToken);
    }
}

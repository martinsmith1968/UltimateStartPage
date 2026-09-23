using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UltimateStartPage.Core.Models;
using UltimateStartPage.Core.Services;

namespace UltimateStartPage.Core.Tests.Fakes
{
    internal sealed class InMemoryLayoutStore : ILayoutStore
    {
        private TaskCompletionSource<StartPageLayout> _nextSave = NewSignal();

        public StartPageLayout Layout { get; set; } = new StartPageLayout();

        public StartPageLayout? LastSaved { get; private set; }

        public int SaveCount { get; private set; }

        public Dictionary<string, StartPageLayout> Files { get; } = new Dictionary<string, StartPageLayout>();

        public string FilePath => @"C:\Users\test\AppData\Roaming\UltimateStartPage\layout.json";

        /// <summary>The "file" was changed by another instance since the view model last loaded or saved it.</summary>
        public bool HasExternalChanges { get; set; }

        public event EventHandler? ExternalChange;

        public Task<StartPageLayout> LoadAsync(CancellationToken cancellationToken)
        {
            HasExternalChanges = false;
            return Task.FromResult(Layout);
        }

        /// <summary>When set, saves don't finish until this completes: a slow disk or network share.</summary>
        public Task? SaveBlocker { get; set; }

        /// <summary>Saves that have started, including ones still held by <see cref="SaveBlocker"/>.</summary>
        public int SaveAttempts;

        public async Task SaveAsync(StartPageLayout layout, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref SaveAttempts);

            if (SaveBlocker != null)
            {
                await SaveBlocker;
            }

            if (HasExternalChanges)
            {
                throw new LayoutConflictException("Changed elsewhere.");
            }

            Record(layout);
        }

        public Task<bool> HasExternalChangesAsync(CancellationToken cancellationToken) => Task.FromResult(HasExternalChanges);

        /// <summary>Simulates another Visual Studio instance saving <paramref name="layout"/>.</summary>
        public void ChangeExternally(StartPageLayout layout)
        {
            Layout = layout;
            HasExternalChanges = true;
            ExternalChange?.Invoke(this, EventArgs.Empty);
        }

        public Task<StartPageLayout> ImportAsync(string path, CancellationToken cancellationToken)
        {
            if (!Files.TryGetValue(path, out var layout))
            {
                throw new LayoutFormatException($"'{path}' is not a valid start page layout.");
            }

            return Task.FromResult(layout);
        }

        public Task ExportAsync(StartPageLayout layout, string path, CancellationToken cancellationToken)
        {
            Files[path] = layout;
            return Task.CompletedTask;
        }

        /// <summary>Completes on the next save. Capture it before triggering the change you expect to be saved.</summary>
        public Task<StartPageLayout> NextSave => _nextSave.Task;

        public static async Task<StartPageLayout> WithTimeout(Task<StartPageLayout> save)
        {
            var completed = await Task.WhenAny(save, Task.Delay(TimeSpan.FromSeconds(5)));
            if (completed != save)
            {
                throw new TimeoutException("The view model never saved.");
            }

            return await save;
        }

        private void Record(StartPageLayout layout)
        {
            // What a later load reads back, as with a real file.
            Layout = layout;
            LastSaved = layout;
            SaveCount++;
            var signal = _nextSave;
            _nextSave = NewSignal();
            signal.TrySetResult(layout);
        }

        private static TaskCompletionSource<StartPageLayout> NewSignal() =>
            new TaskCompletionSource<StartPageLayout>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// Marks "the UI thread" for the synchronous start of a call. Anything that runs synchronously in that call sees
    /// it as <see cref="SynchronizationContext.Current"/>; work moved to the thread pool doesn't.
    /// </summary>
    internal sealed class FakeUiContext : SynchronizationContext
    {
        public static async Task RunAsync(Func<Task> call)
        {
            var previous = Current;
            SetSynchronizationContext(new FakeUiContext());
            Task running;

            try
            {
                running = call();
            }
            finally
            {
                SetSynchronizationContext(previous);
            }

            await running;
        }
    }

    internal sealed class FakeRecentItemsSource : IRecentItemsSource
    {
        public List<RecentItem> Items { get; } = new List<RecentItem>();

        public int ReadCount;

        /// <summary>When set, the next read returns what <see cref="Items"/> held when it started, once this completes.</summary>
        public Task? NextReadBlocker { get; set; }

        public async Task<IReadOnlyList<RecentItem>> GetRecentItemsAsync(int maxItems, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref ReadCount);
            var snapshot = Items.Take(maxItems).ToList();

            var blocker = NextReadBlocker;
            NextReadBlocker = null;
            if (blocker != null)
            {
                await blocker;
            }

            return snapshot;
        }
    }

    internal sealed class FakeLinkLauncher : ILinkLauncher
    {
        public HashSet<string> MissingTargets { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> Folders { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public List<LinkItem> Opened { get; } = new List<LinkItem>();

        /// <summary>True once a disk check ran on the caller's (UI) thread rather than the thread pool.</summary>
        public bool CheckedOnCallersThread { get; private set; }

        public int DiskChecks;

        public bool TargetExists(LinkItem link)
        {
            RecordDiskCheck();
            return !MissingTargets.Contains(link.Target);
        }

        public LinkKind ResolveKind(string target)
        {
            RecordDiskCheck();
            return Folders.Contains(target) ? LinkKind.Folder : LinkKindDetector.Detect(target);
        }

        private void RecordDiskCheck()
        {
            Interlocked.Increment(ref DiskChecks);
            if (SynchronizationContext.Current is FakeUiContext)
            {
                CheckedOnCallersThread = true;
            }
        }

        public Task OpenAsync(LinkItem link, CancellationToken cancellationToken)
        {
            Opened.Add(link);
            return Task.CompletedTask;
        }

        public Task OpenContainingFolderAsync(LinkItem link, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    internal sealed class FakeShellActions : IShellActions
    {
        public List<string> OpenedFiles { get; } = new List<string>();

        public Task OpenProjectOrSolutionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task OpenFolderAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task CloneRepositoryAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task NewProjectAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task OpenFileInEditorAsync(string path, CancellationToken cancellationToken)
        {
            OpenedFiles.Add(path);
            return Task.CompletedTask;
        }

        public Task OpenOptionsAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>Scripted dialogs: queue the answers a test expects the user to give.</summary>
    internal sealed class FakeDialogService : IDialogService
    {
        public Queue<Func<LinkEditRequest, LinkEditResult?>> LinkEdits { get; } = new Queue<Func<LinkEditRequest, LinkEditResult?>>();

        public Queue<string?> TextAnswers { get; } = new Queue<string?>();

        public Queue<bool> Confirmations { get; } = new Queue<bool>();

        public string? ImportPath { get; set; }

        public string? ExportPath { get; set; }

        public List<string> Errors { get; } = new List<string>();

        public List<string> ConfirmMessages { get; } = new List<string>();

        public LinkEditRequest? LastLinkRequest { get; private set; }

        public Task<LinkEditResult?> EditLinkAsync(LinkEditRequest request, CancellationToken cancellationToken)
        {
            LastLinkRequest = request;
            return Task.FromResult(LinkEdits.Dequeue()(request));
        }

        /// <summary>When set, the next text prompt stays open until the test completes this.</summary>
        public TaskCompletionSource<string?>? OpenTextPrompt { get; set; }

        public Task<string?> PromptForTextAsync(string title, string prompt, string initialValue, CancellationToken cancellationToken)
        {
            if (OpenTextPrompt != null)
            {
                var open = OpenTextPrompt;
                OpenTextPrompt = null;
                return open.Task;
            }

            return Task.FromResult(TextAnswers.Dequeue());
        }

        public Task<bool> ConfirmAsync(string title, string message, CancellationToken cancellationToken)
        {
            ConfirmMessages.Add(message);
            return Task.FromResult(Confirmations.Dequeue());
        }

        public Task ShowErrorAsync(string title, string message, CancellationToken cancellationToken)
        {
            Errors.Add(message);
            return Task.CompletedTask;
        }

        public Task<string?> PickLayoutFileToImportAsync(CancellationToken cancellationToken) => Task.FromResult(ImportPath);

        public Task<string?> PickLayoutFileToExportAsync(CancellationToken cancellationToken) => Task.FromResult(ExportPath);
    }

    internal sealed class FakeSettings : IStartPageSettings
    {
        public bool ShowRecentItems { get; set; } = true;

        public int MaxRecentItems { get; set; } = 10;

        public bool ShowGetStartedActions { get; set; } = true;
    }
}

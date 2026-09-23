using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using UltimateStartPage.Core.Models;
using UltimateStartPage.Core.Services;

namespace UltimateStartPage.Core.ViewModels
{
    /// <summary>
    /// Root view model for the start page. All members must be used from the UI thread; any awaits inside
    /// resume on the caller's synchronisation context, and only disk/IO work is pushed to the thread pool.
    /// </summary>
    public sealed class StartPageViewModel : ObservableObject
    {
        private readonly ILayoutStore _layoutStore;
        private readonly IRecentItemsSource _recentItemsSource;
        private readonly ILinkLauncher _launcher;
        private readonly IShellActions _shellActions;
        private readonly IDialogService _dialogs;
        private readonly IStartPageSettings _settings;
        private readonly ILogger<StartPageViewModel> _logger;

        private CancellationTokenSource? _pendingSave;
        private bool _isSaving;
        private Task _currentSave = Task.CompletedTask;

        // The layout as this window last loaded or saved it: the common ancestor when merging with changes made elsewhere.
        private StartPageLayout _ancestor = new StartPageLayout();

        // A merged layout was written while a dialog was open, so the sections shown here weren't swapped for it yet.
        private bool _uiBehindDisk;
        private int _activeOperations;
        private bool _externalChangePending;
        private SynchronizationContext? _uiContext;
        private bool _watchingStore;
        private int _recentItemsRefresh;
        private readonly Stopwatch _sinceRecentItemsRefresh = new Stopwatch();
        private string _searchText = string.Empty;
        private bool _isEditMode;
        private bool _isLoading;
        private bool _showRecentItems;
        private bool _showGetStartedActions;

        public StartPageViewModel(
            ILayoutStore layoutStore,
            IRecentItemsSource recentItemsSource,
            ILinkLauncher launcher,
            IShellActions shellActions,
            IDialogService dialogs,
            IStartPageSettings settings,
            ILogger<StartPageViewModel> logger)
        {
            _layoutStore = layoutStore ?? throw new ArgumentNullException(nameof(layoutStore));
            _recentItemsSource = recentItemsSource ?? throw new ArgumentNullException(nameof(recentItemsSource));
            _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
            _shellActions = shellActions ?? throw new ArgumentNullException(nameof(shellActions));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            Sections.CollectionChanged += (_, __) => OnPropertyChanged(nameof(HasNoSections));

            AddSectionCommand = CreateCommand(_ => AddSectionAsync());
            ToggleEditModeCommand = new RelayCommand(_ => IsEditMode = !IsEditMode);
            ImportCommand = CreateCommand(_ => ImportAsync());
            ExportCommand = CreateCommand(_ => ExportAsync());
            RefreshCommand = CreateCommand(_ => ReloadAsync(CancellationToken.None));
            OpenLayoutFileCommand = CreateCommand(_ => OpenLayoutFileAsync());
            ClearSearchCommand = new RelayCommand(_ => SearchText = string.Empty);
            OpenProjectCommand = CreateCommand(_ => _shellActions.OpenProjectOrSolutionAsync(CancellationToken.None));
            OpenFolderCommand = CreateCommand(_ => _shellActions.OpenFolderAsync(CancellationToken.None));
            CloneRepositoryCommand = CreateCommand(_ => _shellActions.CloneRepositoryAsync(CancellationToken.None));
            NewProjectCommand = CreateCommand(_ => _shellActions.NewProjectAsync(CancellationToken.None));
            OpenOptionsCommand = CreateCommand(_ => _shellActions.OpenOptionsAsync(CancellationToken.None));
        }

        /// <summary>How long edits are batched before being written. Short enough that closing VS rarely loses anything.</summary>
        internal TimeSpan SaveDelay { get; set; } = TimeSpan.FromMilliseconds(400);

        /// <summary>Showing the page re-reads the recent list only if it is older than this (tab switching can be rapid).</summary>
        internal TimeSpan RecentItemsMaxAge { get; set; } = TimeSpan.FromSeconds(2);

        public ObservableCollection<SectionViewModel> Sections { get; } = new ObservableCollection<SectionViewModel>();

        public ObservableCollection<RecentItemViewModel> RecentItems { get; } = new ObservableCollection<RecentItemViewModel>();

        public bool HasNoSections => Sections.Count == 0;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value ?? string.Empty))
                {
                    ApplyFilter();
                }
            }
        }

        public bool IsEditMode
        {
            get => _isEditMode;
            set => SetProperty(ref _isEditMode, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public bool ShowRecentItems
        {
            get => _showRecentItems;
            private set => SetProperty(ref _showRecentItems, value);
        }

        public bool ShowGetStartedActions
        {
            get => _showGetStartedActions;
            private set => SetProperty(ref _showGetStartedActions, value);
        }

        public string LayoutFilePath => _layoutStore.FilePath;

        public ICommand AddSectionCommand { get; }

        public ICommand ToggleEditModeCommand { get; }

        public ICommand ImportCommand { get; }

        public ICommand ExportCommand { get; }

        public ICommand RefreshCommand { get; }

        public ICommand OpenLayoutFileCommand { get; }

        public ICommand ClearSearchCommand { get; }

        public ICommand OpenProjectCommand { get; }

        public ICommand OpenFolderCommand { get; }

        public ICommand CloneRepositoryCommand { get; }

        public ICommand NewProjectCommand { get; }

        public ICommand OpenOptionsCommand { get; }

        /// <summary>Loads everything. Call on the UI thread: outside changes to the layout are marshalled back to it.</summary>
        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            if (!_watchingStore)
            {
                _uiContext = SynchronizationContext.Current;
                _layoutStore.ExternalChange += OnExternalLayoutChange;
                _watchingStore = true;
            }

            await ReloadAsync(cancellationToken);
        }

        /// <summary>Re-reads the layout from disk (picking up hand edits), the recent list and the settings.</summary>
        public async Task ReloadAsync(CancellationToken cancellationToken)
        {
            IsLoading = true;

            try
            {
                await ReloadLayoutAsync(cancellationToken);
                await RefreshRecentItemsAsync(cancellationToken);
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task RefreshRecentItemsAsync(CancellationToken cancellationToken)
        {
            ShowRecentItems = _settings.ShowRecentItems;
            ShowGetStartedActions = _settings.ShowGetStartedActions;

            // Refreshes can overlap (the page being shown while a solution closes); only the latest one may win.
            var refresh = ++_recentItemsRefresh;

            var items = ShowRecentItems
                ? await _recentItemsSource.GetRecentItemsAsync(_settings.MaxRecentItems, cancellationToken)
                : Array.Empty<RecentItem>();

            if (refresh != _recentItemsRefresh)
            {
                return;
            }

            _sinceRecentItemsRefresh.Restart();

            var now = DateTimeOffset.Now;
            var fresh = items.Select(item => new RecentItemViewModel(this, item, now)).ToList();

            // Rebuilding an unchanged list would make it flicker and lose keyboard focus every time the page is shown.
            if (fresh.Select(RecentItemKey).SequenceEqual(RecentItems.Select(RecentItemKey)))
            {
                return;
            }

            RecentItems.Clear();
            foreach (var item in fresh)
            {
                RecentItems.Add(item);
            }

            ApplyFilter();
        }

        /// <summary>
        /// Call when the page becomes visible. Visual Studio's recent list changes whenever something is opened, and
        /// the page can sit in a background tab meanwhile, so re-read it unless that was done moments ago.
        /// </summary>
        public void OnPageShown()
        {
            if (_sinceRecentItemsRefresh.IsRunning && _sinceRecentItemsRefresh.Elapsed < RecentItemsMaxAge)
            {
                return;
            }

            _ = RefreshRecentItemsSafelyAsync();
        }

        private async Task RefreshRecentItemsSafelyAsync()
        {
            try
            {
                await RefreshRecentItemsAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Refreshing the recent items failed");
            }
        }

        private static (string, LinkKind, bool, string) RecentItemKey(RecentItemViewModel item) =>
            (item.FullPath, item.Kind, item.IsFavorite, item.LastAccessedText);

        /// <summary>Writes the layout immediately, whether or not anything changed (so the file exists afterwards).</summary>
        public async Task FlushAsync(CancellationToken cancellationToken)
        {
            CancelPendingSave();
            await SaveNowAsync(cancellationToken);
        }

        /// <summary>True while an edit is waiting out <see cref="SaveDelay"/>, being written, or waiting to be merged.</summary>
        public bool HasUnsavedChanges => _pendingSave != null || _isSaving || _uiBehindDisk;

        /// <summary>
        /// For shutdown: writes an edit still waiting out <see cref="SaveDelay"/>, or waits for a save already under
        /// way. Does nothing if everything is saved, so closing Visual Studio never rewrites an unchanged file.
        /// </summary>
        public async Task SaveUnsavedChangesAsync(CancellationToken cancellationToken)
        {
            if (_pendingSave != null || (_uiBehindDisk && !_isSaving))
            {
                _logger.LogInformation("Saving unsaved start page changes before closing");
                await FlushAsync(cancellationToken);
                return;
            }

            if (_isSaving)
            {
                // Its errors were already logged by whoever started it.
                await Task.WhenAny(_currentSave, Task.Delay(Timeout.Infinite, cancellationToken));
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        /// <summary>For a drop handler, which can't await: adds the paths in the background, reporting any failure.</summary>
        public void AddDroppedPaths(SectionViewModel section, IEnumerable<string> paths)
        {
            var dropped = paths.ToList();
            CreateCommand(_ => AddDroppedPathsAsync(section, dropped)).Execute(null);
        }

        /// <summary>Adds files or folders dropped onto a section (e.g. dragged from Explorer). Duplicates are skipped.</summary>
        internal async Task AddDroppedPathsAsync(SectionViewModel section, IReadOnlyList<string> paths)
        {
            var added = 0;
            var candidates = paths
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Where(p => !section.Links.Any(l => string.Equals(l.Target, p, StringComparison.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Telling a folder called "My.Repo" from a file means asking the disk, which may be a slow network share.
            var kinds = await Task.Run(() => candidates.Select(_launcher.ResolveKind).ToList());

            for (var i = 0; i < candidates.Count; i++)
            {
                var path = candidates[i];
                var kind = kinds[i];

                // The section may have gained the same link while the disk was being checked.
                if (section.Links.Any(l => string.Equals(l.Target, path, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var link = new LinkItem
                {
                    Title = LinkKindDetector.SuggestTitle(path, kind),
                    Target = path,
                    Kind = kind,
                };
                section.Links.Add(new LinkViewModel(this, section, link));
                added++;
            }

            if (added > 0)
            {
                _logger.LogInformation("Added {LinkCount} dropped links to section {SectionTitle}", added, section.Title);
                ApplyFilter();
                RequestSave();
            }
        }

        internal AsyncRelayCommand CreateCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null)
        {
            return new AsyncRelayCommand(parameter => RunUserOperationAsync(() => execute(parameter)), OnCommandFailed, canExecute);
        }

        internal void RequestSave()
        {
            CancelPendingSave();
            var pendingSave = new CancellationTokenSource();
            _pendingSave = pendingSave;
            _ = SaveAfterDelayAsync(pendingSave);
        }

        internal async Task AddSectionAsync()
        {
            var title = await _dialogs.PromptForTextAsync("New section", "Section name:", string.Empty, CancellationToken.None);
            if (string.IsNullOrWhiteSpace(title))
            {
                return;
            }

            Sections.Add(new SectionViewModel(this, new Section { Title = title!.Trim() }));
            RequestSave();
        }

        internal async Task RenameSectionAsync(SectionViewModel section)
        {
            var title = await _dialogs.PromptForTextAsync("Rename section", "Section name:", section.Title, CancellationToken.None);
            if (!string.IsNullOrWhiteSpace(title))
            {
                section.Title = title!.Trim();
            }
        }

        internal async Task DeleteSectionAsync(SectionViewModel section)
        {
            if (section.Links.Count > 0)
            {
                var confirmed = await _dialogs.ConfirmAsync(
                    "Delete section",
                    $"Delete \"{section.Title}\" and its {section.Links.Count} link(s)?",
                    CancellationToken.None);

                if (!confirmed)
                {
                    return;
                }
            }

            Sections.Remove(section);
            RequestSave();
        }

        internal void MoveSection(SectionViewModel section, int delta)
        {
            var index = Sections.IndexOf(section);
            var newIndex = index + delta;

            if (index < 0 || newIndex < 0 || newIndex >= Sections.Count)
            {
                return;
            }

            Sections.Move(index, newIndex);
            RequestSave();
        }

        internal async Task AddLinkAsync(SectionViewModel section)
        {
            var request = CreateRequest("Add link", section);
            var result = await _dialogs.EditLinkAsync(request, CancellationToken.None);
            if (result == null)
            {
                return;
            }

            var target = SectionAt(result.SectionIndex) ?? section;
            target.Links.Add(new LinkViewModel(this, target, ToModel(result)));
            ApplyFilter();
            RequestSave();
            await RefreshMissingFlagsAsync(CancellationToken.None);
        }

        internal async Task EditLinkAsync(LinkViewModel link)
        {
            var request = CreateRequest("Edit link", link.Section);
            request.Title = link.Title;
            request.Target = link.Target;
            request.Description = link.Description;

            var result = await _dialogs.EditLinkAsync(request, CancellationToken.None);
            if (result == null)
            {
                return;
            }

            link.Title = result.Title;
            link.Target = result.Target;
            link.Kind = result.Kind;
            link.Description = result.Description;

            var destination = SectionAt(result.SectionIndex);
            if (destination != null && destination != link.Section)
            {
                link.Section.Links.Remove(link);
                destination.Links.Add(link);
                link.Section = destination;
            }

            ApplyFilter();
            RequestSave();
            await RefreshMissingFlagsAsync(CancellationToken.None);
        }

        internal async Task RemoveLinkAsync(LinkViewModel link)
        {
            var confirmed = await _dialogs.ConfirmAsync(
                "Remove link", $"Remove \"{link.Title}\" from \"{link.Section.Title}\"?", CancellationToken.None);

            if (confirmed)
            {
                link.Section.Links.Remove(link);
                RequestSave();
            }
        }

        internal void MoveLink(LinkViewModel link, int delta)
        {
            var links = link.Section.Links;
            var index = links.IndexOf(link);
            var newIndex = index + delta;

            if (index < 0 || newIndex < 0 || newIndex >= links.Count)
            {
                return;
            }

            links.Move(index, newIndex);
            RequestSave();
        }

        internal async Task OpenLinkAsync(LinkViewModel link)
        {
            var model = link.ToModel();

            if (!await TargetExistsAsync(model))
            {
                link.IsMissing = true;

                var remove = await _dialogs.ConfirmAsync(
                    "Link not found",
                    $"\"{link.Target}\" could not be found. It may have been moved or deleted.\n\nRemove \"{link.Title}\" from the start page?",
                    CancellationToken.None);

                if (remove)
                {
                    link.Section.Links.Remove(link);
                    RequestSave();
                }

                return;
            }

            link.IsMissing = false;
            _logger.LogInformation("Opening {LinkKind} {LinkTarget}", model.Kind, model.Target);
            await _launcher.OpenAsync(model, CancellationToken.None);
        }

        internal Task OpenContainingFolderAsync(LinkViewModel link)
        {
            return _launcher.OpenContainingFolderAsync(link.ToModel(), CancellationToken.None);
        }

        internal async Task OpenRecentAsync(RecentItemViewModel recent)
        {
            var model = recent.ToLinkItem();

            if (!await TargetExistsAsync(model))
            {
                await _dialogs.ShowErrorAsync(
                    "Not found", $"\"{recent.FullPath}\" could not be found.", CancellationToken.None);
                return;
            }

            await _launcher.OpenAsync(model, CancellationToken.None);
        }

        internal async Task PinRecentAsync(RecentItemViewModel recent)
        {
            if (Sections.Count == 0)
            {
                Sections.Add(new SectionViewModel(this, new Section { Title = "Favourites" }));
            }

            var request = new LinkEditRequest("Pin to section", SectionTitles(), 0)
            {
                Title = recent.Title,
                Target = recent.FullPath,
            };

            var result = await _dialogs.EditLinkAsync(request, CancellationToken.None);
            if (result == null)
            {
                return;
            }

            var section = SectionAt(result.SectionIndex) ?? Sections[0];
            section.Links.Add(new LinkViewModel(this, section, ToModel(result)));
            ApplyFilter();
            RequestSave();
        }

        internal async Task ImportAsync()
        {
            var path = await _dialogs.PickLayoutFileToImportAsync(CancellationToken.None);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            StartPageLayout imported;
            try
            {
                imported = await _layoutStore.ImportAsync(path!, CancellationToken.None);
            }
            catch (LayoutFormatException ex)
            {
                await _dialogs.ShowErrorAsync("Import failed", ex.Message, CancellationToken.None);
                return;
            }

            var confirmed = await _dialogs.ConfirmAsync(
                "Import layout",
                $"Add {imported.Sections.Count} section(s) from \"{path}\" to your start page? Your existing sections are kept.",
                CancellationToken.None);

            if (!confirmed)
            {
                return;
            }

            foreach (var section in imported.Sections)
            {
                // Fresh ids so importing the same file twice never produces clashing sections.
                section.Id = Guid.NewGuid();
                section.Links.ForEach(l => l.Id = Guid.NewGuid());
                Sections.Add(new SectionViewModel(this, section));
            }

            ApplyFilter();
            RequestSave();
            await RefreshMissingFlagsAsync(CancellationToken.None);
        }

        internal async Task ExportAsync()
        {
            var path = await _dialogs.PickLayoutFileToExportAsync(CancellationToken.None);
            if (!string.IsNullOrWhiteSpace(path))
            {
                await _layoutStore.ExportAsync(ToModel(), path!, CancellationToken.None);
            }
        }

        internal async Task OpenLayoutFileAsync()
        {
            await FlushAsync(CancellationToken.None);
            await _shellActions.OpenFileInEditorAsync(_layoutStore.FilePath, CancellationToken.None);
        }

        internal StartPageLayout ToModel()
        {
            return new StartPageLayout { Sections = Sections.Select(s => s.ToModel()).ToList() };
        }

        /// <summary>Replaces the sections with what is on disk, discarding any edits not yet saved.</summary>
        private async Task ReloadLayoutAsync(CancellationToken cancellationToken)
        {
            CancelPendingSave();
            _externalChangePending = false;

            var layout = await _layoutStore.LoadAsync(cancellationToken);
            ReplaceSections(layout);
            await RefreshMissingFlagsAsync(cancellationToken);
        }

        private void ReplaceSections(StartPageLayout layout)
        {
            ShowSections(layout);
            _ancestor = ToModel();
            _uiBehindDisk = false;
        }

        private void ShowSections(StartPageLayout layout)
        {
            Sections.Clear();
            foreach (var section in layout.Sections)
            {
                Sections.Add(new SectionViewModel(this, section));
            }

            ApplyFilter();
        }

        /// <summary>File.Exists on an unreachable network share can take many seconds, so never run it on the UI thread.</summary>
        private Task<bool> TargetExistsAsync(LinkItem link) => Task.Run(() => _launcher.TargetExists(link));

        private async Task RefreshMissingFlagsAsync(CancellationToken cancellationToken)
        {
            var links = Sections.SelectMany(s => s.Links).ToList();
            var models = links.Select(l => l.ToModel()).ToList();

            // See TargetExistsAsync.
            var missingIds = await Task.Run(
                () => new HashSet<Guid>(models.Where(m => !_launcher.TargetExists(m)).Select(m => m.Id)),
                cancellationToken);

            foreach (var link in links)
            {
                link.IsMissing = missingIds.Contains(link.Id);
            }
        }

        private void ApplyFilter()
        {
            foreach (var section in Sections)
            {
                var sectionMatches = SearchMatcher.Matches(SearchText, section.Title);

                foreach (var link in section.Links)
                {
                    link.IsVisible = sectionMatches
                        || SearchMatcher.Matches(SearchText, link.Title, link.Target, link.Description, section.Title);
                }

                section.IsVisible = string.IsNullOrWhiteSpace(SearchText)
                    || sectionMatches
                    || section.Links.Any(l => l.IsVisible);
            }

            foreach (var recent in RecentItems)
            {
                recent.IsVisible = SearchMatcher.Matches(SearchText, recent.Title, recent.FullPath);
            }
        }

        private async Task SaveAfterDelayAsync(CancellationTokenSource pendingSave)
        {
            try
            {
                await Task.Delay(SaveDelay, pendingSave.Token);

                // Now under way rather than pending: an edit made during the save schedules another one.
                if (_pendingSave == pendingSave)
                {
                    _pendingSave = null;
                }

                await SaveNowAsync(pendingSave.Token);
            }
            catch (OperationCanceledException)
            {
                // Superseded by a newer edit or a reload.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Saving the start page layout to {LayoutPath} failed", _layoutStore.FilePath);
            }
            finally
            {
                pendingSave.Dispose();
            }
        }

        private Task SaveNowAsync(CancellationToken cancellationToken) => TrackSave(SaveOrMergeAsync(cancellationToken, mergeFirst: false));

        /// <summary>Brings this window and the file together: merges, writes the result if needed, and shows it.</summary>
        private Task MergeWithFileNowAsync(CancellationToken cancellationToken) => TrackSave(SaveOrMergeAsync(cancellationToken, mergeFirst: true));

        private Task TrackSave(Task save)
        {
            _currentSave = save;
            return save;
        }

        private async Task SaveOrMergeAsync(CancellationToken cancellationToken, bool mergeFirst)
        {
            _isSaving = true;

            try
            {
                if (!mergeFirst && !_uiBehindDisk)
                {
                    // Snapshot on the UI thread; the store does the IO.
                    var snapshot = ToModel();

                    try
                    {
                        await _layoutStore.SaveAsync(snapshot, cancellationToken);
                        _ancestor = snapshot;
                        return;
                    }
                    catch (LayoutConflictException ex)
                    {
                        _logger.LogInformation("Merging with changes made elsewhere: {Reason}", ex.Message);
                    }
                }

                await MergeWithFileAsync(cancellationToken);
            }
            finally
            {
                _isSaving = false;
                ProcessDeferredWork();
            }
        }

        /// <summary>
        /// Three-way merges this window's sections with the file, using the layout this window last loaded or saved
        /// as the common ancestor, then writes the result if it adds anything. See <see cref="LayoutMerger"/>.
        /// </summary>
        private async Task MergeWithFileAsync(CancellationToken cancellationToken)
        {
            const int maxAttempts = 3;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                // Loading resets the store's idea of what's on disk to "theirs", so from here the merged result
                // must be written: saving this window's sections unmerged would now overwrite theirs unchecked.
                var theirs = await _layoutStore.LoadAsync(cancellationToken);
                var mine = ToModel();
                var merge = LayoutMerger.Merge(_ancestor, mine, theirs);

                foreach (var conflict in merge.Conflicts)
                {
                    _logger.LogWarning("Merging the start page: {Conflict}", conflict);
                }

                if (!LayoutMerger.AreEquivalent(merge.Layout, theirs))
                {
                    try
                    {
                        await _layoutStore.SaveAsync(merge.Layout, cancellationToken);
                    }
                    catch (LayoutConflictException)
                    {
                        _logger.LogDebug("The layout changed again while merging (attempt {Attempt})", attempt);
                        continue;
                    }
                }

                if (_activeOperations > 0)
                {
                    // A dialog may be holding on to the current view models, and whatever it does to them would be
                    // lost if they were swapped out now. Swap once it closes, merging again against what was just saved.
                    _ancestor = mine;
                    _uiBehindDisk = true;
                    return;
                }

                _logger.LogInformation("Merged the start page with changes made elsewhere");
                ReplaceSections(merge.Layout);
                await RefreshMissingFlagsAsync(cancellationToken);
                return;
            }

            // Something keeps rewriting the file. Keep the edits and try again shortly.
            _logger.LogWarning("{LayoutPath} kept changing while merging; will retry", _layoutStore.FilePath);
            _uiBehindDisk = true;
            RequestSave();
        }

        private void CancelPendingSave()
        {
            // SaveAfterDelayAsync owns the token source and disposes it.
            _pendingSave?.Cancel();
            _pendingSave = null;
        }

        /// <summary>A command, a save or an unsaved edit that a reload would interfere with.</summary>
        private bool HasLocalWorkInProgress => _activeOperations > 0 || _isSaving || _pendingSave != null;

        /// <summary>Tracks commands (which may be showing a dialog) so an outside change doesn't reload under them.</summary>
        private async Task RunUserOperationAsync(Func<Task> operation)
        {
            _activeOperations++;

            try
            {
                await operation();
            }
            finally
            {
                _activeOperations--;
                ProcessDeferredWork();
            }
        }

        private void OnExternalLayoutChange(object? sender, EventArgs e)
        {
            // Raised on a background thread; everything here touches UI-bound collections.
            if (_uiContext != null)
            {
                _uiContext.Post(_ => _ = HandleExternalChangeAsync(), null);
            }
            else
            {
                _ = HandleExternalChangeAsync();
            }
        }

        /// <summary>Picks up an outside change, or a merge the UI couldn't show yet, once nothing is in the way.</summary>
        private void ProcessDeferredWork()
        {
            if ((_externalChangePending || _uiBehindDisk) && !HasLocalWorkInProgress)
            {
                _ = HandleExternalChangeAsync();
            }
        }

        private async Task HandleExternalChangeAsync()
        {
            try
            {
                // Swapping sections now would pull them out from under an open dialog, and a pending save will merge
                // anyway. Otherwise this runs again once the work is done.
                if (HasLocalWorkInProgress)
                {
                    _externalChangePending = true;
                    return;
                }

                _externalChangePending = false;

                // By now this window may already have merged or saved over the change.
                if (!_uiBehindDisk && !await _layoutStore.HasExternalChangesAsync(CancellationToken.None))
                {
                    return;
                }

                if (HasLocalWorkInProgress)
                {
                    _externalChangePending = true;
                    return;
                }

                _logger.LogInformation("Updating the start page because {LayoutPath} was changed elsewhere", _layoutStore.FilePath);
                await MergeWithFileNowAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reloading the changed layout from {LayoutPath} failed", _layoutStore.FilePath);
            }
        }

        private LinkEditRequest CreateRequest(string dialogTitle, SectionViewModel section)
        {
            return new LinkEditRequest(dialogTitle, SectionTitles(), Math.Max(0, Sections.IndexOf(section)));
        }

        private IReadOnlyList<string> SectionTitles() => Sections.Select(s => s.Title).ToList();

        private SectionViewModel? SectionAt(int index) => index >= 0 && index < Sections.Count ? Sections[index] : null;

        private static LinkItem ToModel(LinkEditResult result)
        {
            return new LinkItem
            {
                Title = result.Title,
                Target = result.Target,
                Kind = result.Kind,
                Description = result.Description,
            };
        }

        private void OnCommandFailed(Exception exception)
        {
            _logger.LogError(exception, "A start page command failed");
            _ = _dialogs.ShowErrorAsync("Ultimate Start Page", exception.Message, CancellationToken.None);
        }
    }
}

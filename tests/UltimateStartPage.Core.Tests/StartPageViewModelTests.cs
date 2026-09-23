using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using UltimateStartPage.Core.Models;
using UltimateStartPage.Core.Services;
using UltimateStartPage.Core.Tests.Fakes;
using UltimateStartPage.Core.ViewModels;
using Xunit;

namespace UltimateStartPage.Core.Tests
{
    public class StartPageViewModelTests
    {
        private const string HarnessSln = @"C:\Dev\Harness\SS.Modelling.Harness.sln";
        private const string ToolsSln = @"C:\Dev\Tools\Tools.sln";

        private readonly InMemoryLayoutStore _store = new InMemoryLayoutStore();
        private readonly FakeRecentItemsSource _recent = new FakeRecentItemsSource();
        private readonly FakeLinkLauncher _launcher = new FakeLinkLauncher();
        private readonly FakeShellActions _shell = new FakeShellActions();
        private readonly FakeDialogService _dialogs = new FakeDialogService();
        private readonly FakeSettings _settings = new FakeSettings();

        public StartPageViewModelTests()
        {
            _store.Layout = new StartPageLayout
            {
                Sections = new List<Section>
                {
                    new Section
                    {
                        Title = "Work",
                        Links = new List<LinkItem>
                        {
                            new LinkItem { Title = "Harness", Target = HarnessSln, Kind = LinkKind.Solution },
                            new LinkItem { Title = "Tools", Target = ToolsSln, Kind = LinkKind.Solution },
                        },
                    },
                    new Section { Title = "Personal" },
                },
            };
        }

        [Fact]
        public async Task InitializeAsync_LoadsSectionsRecentItemsAndMissingFlags()
        {
            _launcher.MissingTargets.Add(ToolsSln);
            _recent.Items.Add(new RecentItem(@"C:\Dev\Other\Other.sln", LinkKind.Solution, DateTimeOffset.Now, false));
            var vm = CreateViewModel();

            await vm.InitializeAsync(CancellationToken.None);

            Assert.Equal(new[] { "Work", "Personal" }, vm.Sections.Select(s => s.Title).ToArray());
            Assert.False(vm.Sections[0].Links[0].IsMissing);
            Assert.True(vm.Sections[0].Links[1].IsMissing);
            Assert.Equal("Other", Assert.Single(vm.RecentItems).Title);
            Assert.True(vm.ShowRecentItems);
        }

        [Fact]
        public async Task InitializeAsync_WhenRecentItemsDisabled_ShowsNone()
        {
            _settings.ShowRecentItems = false;
            _recent.Items.Add(new RecentItem(@"C:\Dev\Other\Other.sln", LinkKind.Solution, null, false));
            var vm = CreateViewModel();

            await vm.InitializeAsync(CancellationToken.None);

            Assert.False(vm.ShowRecentItems);
            Assert.Empty(vm.RecentItems);
        }

        [Fact]
        public async Task EditingATitle_SavesAutomatically()
        {
            var vm = await CreateInitialisedViewModelAsync();
            vm.SaveDelay = TimeSpan.Zero;
            var save = _store.NextSave;

            vm.Sections[0].Title = "Day job";

            var saved = await InMemoryLayoutStore.WithTimeout(save);
            Assert.Equal("Day job", saved.Sections[0].Title);
        }

        [Fact]
        public async Task RapidEdits_AreBatchedIntoOneSave()
        {
            var vm = await CreateInitialisedViewModelAsync();
            vm.SaveDelay = TimeSpan.FromMilliseconds(200);

            vm.Sections[0].Title = "A";
            vm.Sections[0].Title = "AB";
            vm.Sections[0].Title = "ABC";
            await Task.Delay(600);

            Assert.Equal(1, _store.SaveCount);
            Assert.Equal("ABC", _store.LastSaved!.Sections[0].Title);
        }

        [Fact]
        public async Task AddSection_UsesPromptedName()
        {
            var vm = await CreateInitialisedViewModelAsync();
            _dialogs.TextAnswers.Enqueue("  Upside  ");

            await vm.AddSectionAsync();
            await vm.FlushAsync(CancellationToken.None);

            Assert.Equal("Upside", vm.Sections.Last().Title);
            Assert.Equal(3, _store.LastSaved!.Sections.Count);
        }

        [Fact]
        public async Task AddSection_WhenCancelled_AddsNothing()
        {
            var vm = await CreateInitialisedViewModelAsync();
            _dialogs.TextAnswers.Enqueue(null);

            await vm.AddSectionAsync();

            Assert.Equal(2, vm.Sections.Count);
        }

        [Fact]
        public async Task AddLink_PlacesLinkInSectionChosenInDialog()
        {
            var vm = await CreateInitialisedViewModelAsync();
            _dialogs.LinkEdits.Enqueue(r => new LinkEditResult("Upside", @"C:\Dev\Upside\Upside.sln", LinkKind.Solution, null, 1));

            await vm.AddLinkAsync(vm.Sections[0]);

            Assert.Equal(0, _dialogs.LastLinkRequest!.SelectedSectionIndex);
            Assert.Equal(new[] { "Work", "Personal" }, _dialogs.LastLinkRequest.SectionTitles.ToArray());
            Assert.Equal("Upside", Assert.Single(vm.Sections[1].Links).Title);
            Assert.Equal(2, vm.Sections[0].Links.Count);
        }

        [Fact]
        public async Task EditLink_UpdatesFieldsAndCanMoveBetweenSections()
        {
            var vm = await CreateInitialisedViewModelAsync();
            var link = vm.Sections[0].Links[0];
            _dialogs.LinkEdits.Enqueue(r =>
            {
                Assert.Equal("Harness", r.Title);
                Assert.Equal(HarnessSln, r.Target);
                return new LinkEditResult("Harness (main)", r.Target, LinkKind.Solution, "Pricing engine", 1);
            });

            await vm.EditLinkAsync(link);

            Assert.Same(link, Assert.Single(vm.Sections[1].Links));
            Assert.Same(vm.Sections[1], link.Section);
            Assert.Equal("Harness (main)", link.Title);
            Assert.Equal("Pricing engine" + Environment.NewLine + HarnessSln, link.ToolTip);
            Assert.Single(vm.Sections[0].Links);
        }

        [Fact]
        public async Task RemoveLink_OnlyWhenConfirmed()
        {
            var vm = await CreateInitialisedViewModelAsync();
            _dialogs.Confirmations.Enqueue(false);
            _dialogs.Confirmations.Enqueue(true);

            await vm.RemoveLinkAsync(vm.Sections[0].Links[0]);
            Assert.Equal(2, vm.Sections[0].Links.Count);

            await vm.RemoveLinkAsync(vm.Sections[0].Links[0]);
            Assert.Equal("Tools", Assert.Single(vm.Sections[0].Links).Title);
        }

        [Fact]
        public async Task OpenLink_WhenTargetExists_LaunchesIt()
        {
            var vm = await CreateInitialisedViewModelAsync();

            await vm.OpenLinkAsync(vm.Sections[0].Links[0]);

            Assert.Equal(HarnessSln, Assert.Single(_launcher.Opened).Target);
        }

        [Fact]
        public async Task OpenLink_WhenTargetMissing_OffersToRemoveInsteadOfLaunching()
        {
            var vm = await CreateInitialisedViewModelAsync();
            _launcher.MissingTargets.Add(HarnessSln);
            _dialogs.Confirmations.Enqueue(true);

            await vm.OpenLinkAsync(vm.Sections[0].Links[0]);

            Assert.Empty(_launcher.Opened);
            Assert.Contains("could not be found", Assert.Single(_dialogs.ConfirmMessages));
            Assert.Equal("Tools", Assert.Single(vm.Sections[0].Links).Title);
        }

        [Fact]
        public async Task MoveLinkAndSection_ReorderAndIgnoreMovesPastTheEnds()
        {
            var vm = await CreateInitialisedViewModelAsync();

            vm.MoveLink(vm.Sections[0].Links[0], +1);
            vm.MoveLink(vm.Sections[0].Links[1], +1);
            vm.MoveSection(vm.Sections[0], -1);
            vm.MoveSection(vm.Sections[1], -1);

            Assert.Equal(new[] { "Tools", "Harness" }, vm.Sections[1].Links.Select(l => l.Title).ToArray());
            Assert.Equal(new[] { "Personal", "Work" }, vm.Sections.Select(s => s.Title).ToArray());
        }

        [Fact]
        public async Task DeleteSection_WithLinks_RequiresConfirmation_EmptySectionDoesNot()
        {
            var vm = await CreateInitialisedViewModelAsync();
            _dialogs.Confirmations.Enqueue(false);

            await vm.DeleteSectionAsync(vm.Sections[0]);
            await vm.DeleteSectionAsync(vm.Sections[1]);

            Assert.Equal("Work", Assert.Single(vm.Sections).Title);
            Assert.Single(_dialogs.ConfirmMessages);
        }

        [Fact]
        public async Task AddDroppedPaths_SkipsDuplicatesAndResolvesFolders()
        {
            var vm = await CreateInitialisedViewModelAsync();
            _launcher.Folders.Add(@"C:\Dev\My.Repo");

            await vm.AddDroppedPathsAsync(vm.Sections[0], new[] { HarnessSln.ToUpperInvariant(), @"C:\Dev\My.Repo", @"C:\Dev\Api\Api.csproj", "" });

            var titles = vm.Sections[0].Links.Select(l => l.Title).ToArray();
            Assert.Equal(new[] { "Harness", "Tools", "My.Repo", "Api" }, titles);
            Assert.Equal(LinkKind.Folder, vm.Sections[0].Links[2].Kind);
            Assert.Equal(LinkKind.Project, vm.Sections[0].Links[3].Kind);
        }

        [Fact]
        public async Task SearchText_FiltersLinksSectionsAndRecentItems()
        {
            _recent.Items.Add(new RecentItem(@"C:\Dev\Tennis\Tennis.sln", LinkKind.Solution, null, false));
            var vm = await CreateInitialisedViewModelAsync();

            vm.SearchText = "harness";

            Assert.True(vm.Sections[0].IsVisible);
            Assert.True(vm.Sections[0].Links[0].IsVisible);
            Assert.False(vm.Sections[0].Links[1].IsVisible);
            Assert.False(vm.Sections[1].IsVisible);
            Assert.False(vm.RecentItems[0].IsVisible);

            vm.SearchText = "personal";
            Assert.True(vm.Sections[1].IsVisible);

            vm.ClearSearchCommand.Execute(null);
            Assert.All(vm.Sections, s => Assert.True(s.IsVisible));
            Assert.All(vm.Sections[0].Links, l => Assert.True(l.IsVisible));
        }

        [Fact]
        public async Task PinRecent_CopiesItIntoChosenSection()
        {
            _recent.Items.Add(new RecentItem(@"C:\Dev\My.Repo", LinkKind.Folder, null, false));
            var vm = await CreateInitialisedViewModelAsync();
            _dialogs.LinkEdits.Enqueue(r => new LinkEditResult(r.Title, r.Target, LinkKind.Folder, null, r.SelectedSectionIndex + 1));

            await vm.PinRecentAsync(vm.RecentItems[0]);

            Assert.Equal("My.Repo", _dialogs.LastLinkRequest!.Title);
            Assert.Equal(@"C:\Dev\My.Repo", Assert.Single(vm.Sections[1].Links).Target);
        }

        [Fact]
        public async Task PinRecent_WhenNoSections_CreatesFavourites()
        {
            _store.Layout = new StartPageLayout();
            _recent.Items.Add(new RecentItem(HarnessSln, LinkKind.Solution, null, false));
            var vm = await CreateInitialisedViewModelAsync();
            _dialogs.LinkEdits.Enqueue(r => new LinkEditResult(r.Title, r.Target, LinkKind.Solution, null, 0));

            await vm.PinRecentAsync(vm.RecentItems[0]);

            var section = Assert.Single(vm.Sections);
            Assert.Equal("Favourites", section.Title);
            Assert.Single(section.Links);
        }

        [Fact]
        public async Task Import_AppendsSectionsWithFreshIds_WhenConfirmed()
        {
            var vm = await CreateInitialisedViewModelAsync();
            var shared = new StartPageLayout
            {
                Sections = new List<Section>
                {
                    new Section { Title = "Team", Links = new List<LinkItem> { new LinkItem { Title = "Harness", Target = HarnessSln } } },
                },
            };
            _store.Files[@"C:\share\team.json"] = shared;
            _dialogs.ImportPath = @"C:\share\team.json";
            _dialogs.Confirmations.Enqueue(true);
            _dialogs.Confirmations.Enqueue(true);

            await vm.ImportAsync();
            await vm.ImportAsync();

            Assert.Equal(new[] { "Work", "Personal", "Team", "Team" }, vm.Sections.Select(s => s.Title).ToArray());
            Assert.NotEqual(vm.Sections[2].Id, vm.Sections[3].Id);
            Assert.NotEqual(vm.Sections[2].Links[0].Id, vm.Sections[3].Links[0].Id);
        }

        [Fact]
        public async Task Import_WhenFileInvalid_ShowsErrorAndChangesNothing()
        {
            var vm = await CreateInitialisedViewModelAsync();
            _dialogs.ImportPath = @"C:\share\broken.json";

            await vm.ImportAsync();

            Assert.Single(_dialogs.Errors);
            Assert.Equal(2, vm.Sections.Count);
        }

        [Fact]
        public async Task Export_WritesCurrentLayout()
        {
            var vm = await CreateInitialisedViewModelAsync();
            _dialogs.ExportPath = @"C:\share\mine.json";

            await vm.ExportAsync();

            Assert.Equal(2, _store.Files[@"C:\share\mine.json"].Sections.Count);
        }

        [Fact]
        public async Task OpenLayoutFile_FlushesThenOpensJsonInEditor()
        {
            var vm = await CreateInitialisedViewModelAsync();
            vm.SaveDelay = TimeSpan.FromMinutes(1);
            vm.Sections[0].Title = "Unsaved";

            await vm.OpenLayoutFileAsync();

            Assert.Equal("Unsaved", _store.LastSaved!.Sections[0].Title);
            Assert.Equal(_store.FilePath, Assert.Single(_shell.OpenedFiles));
        }

        [Fact]
        public async Task FailingCommand_ReportsErrorInsteadOfThrowing()
        {
            var vm = await CreateInitialisedViewModelAsync();
            var command = vm.CreateCommand(_ => throw new InvalidOperationException("boom"));

            await command.ExecuteAsync(null);

            Assert.Equal("boom", Assert.Single(_dialogs.Errors));
            Assert.True(command.CanExecute(null));
        }

        [Fact]
        public async Task ExternalChange_WhenIdle_ReloadsSections()
        {
            var vm = await CreateInitialisedViewModelAsync();

            _store.ChangeExternally(LayoutTitled("Theirs"));

            await WaitUntilAsync(() => vm.Sections.Count == 1 && vm.Sections[0].Title == "Theirs");
            Assert.Equal(0, _store.SaveCount);
        }

        [Fact]
        public async Task ExternalChange_WhileADialogIsOpen_WaitsForItToClose()
        {
            var vm = await CreateInitialisedViewModelAsync();
            var prompt = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _dialogs.OpenTextPrompt = prompt;
            var addSection = ((AsyncRelayCommand)vm.AddSectionCommand).ExecuteAsync(null);

            _store.ChangeExternally(LayoutTitled("Theirs"));
            await Task.Delay(200);
            Assert.Equal(new[] { "Work", "Personal" }, vm.Sections.Select(s => s.Title).ToArray());

            prompt.SetResult(null);
            await addSection;

            await WaitUntilAsync(() => vm.Sections.Count == 1 && vm.Sections[0].Title == "Theirs");
        }

        [Fact]
        public async Task SaveConflict_MergesBothWindowsChangesWithoutAsking()
        {
            var vm = await CreateInitialisedViewModelAsync();
            ChangeOnDiskElsewhere(theirs => theirs.Sections.Add(new Section { Title = "Theirs" }));

            vm.Sections[0].Title = "Mine";
            await vm.FlushAsync(CancellationToken.None);

            Assert.Equal(new[] { "Mine", "Personal", "Theirs" }, vm.Sections.Select(s => s.Title).ToArray());
            Assert.Equal(new[] { "Mine", "Personal", "Theirs" }, _store.LastSaved!.Sections.Select(s => s.Title).ToArray());
            Assert.Empty(_dialogs.ConfirmMessages);
            Assert.False(vm.HasUnsavedChanges);
        }

        [Fact]
        public async Task SaveConflict_WhenBothRenameTheSameSection_ThisWindowWins()
        {
            var vm = await CreateInitialisedViewModelAsync();
            ChangeOnDiskElsewhere(theirs => theirs.Sections[0].Title = "Theirs");

            vm.Sections[0].Title = "Mine";
            await vm.FlushAsync(CancellationToken.None);

            Assert.Equal("Mine", vm.Sections[0].Title);
            Assert.Equal("Mine", _store.LastSaved!.Sections[0].Title);
        }

        [Fact]
        public async Task SaveConflict_WhileADialogIsOpen_SavesTheMergeButSwapsSectionsAfterwards()
        {
            var vm = await CreateInitialisedViewModelAsync();
            var prompt = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _dialogs.OpenTextPrompt = prompt;
            var addSection = ((AsyncRelayCommand)vm.AddSectionCommand).ExecuteAsync(null);
            ChangeOnDiskElsewhere(theirs => theirs.Sections.Add(new Section { Title = "Theirs" }));

            vm.Sections[0].Title = "Mine";
            await vm.FlushAsync(CancellationToken.None);

            // On disk straight away, but the dialog's view models are left alone...
            Assert.Equal(new[] { "Mine", "Personal", "Theirs" }, _store.LastSaved!.Sections.Select(s => s.Title).ToArray());
            Assert.Equal(2, vm.Sections.Count);
            Assert.True(vm.HasUnsavedChanges);

            // ...and what the dialog did is merged in too once it closes.
            prompt.SetResult("New");
            await addSection;
            await WaitUntilAsync(() => vm.Sections.Count == 4);
            Assert.Equal(new[] { "Mine", "Personal", "Theirs", "New" }, vm.Sections.Select(s => s.Title).ToArray());
            await WaitUntilAsync(() => !vm.HasUnsavedChanges);
            Assert.Equal(new[] { "Mine", "Personal", "Theirs", "New" }, _store.LastSaved!.Sections.Select(s => s.Title).ToArray());
        }

        [Fact]
        public async Task OnPageShown_PicksUpSolutionsOpenedSinceTheLastRefresh()
        {
            var vm = await CreateInitialisedViewModelAsync();
            vm.RecentItemsMaxAge = TimeSpan.Zero;
            _recent.Items.Add(new RecentItem(ToolsSln, LinkKind.Solution, DateTimeOffset.Now, false));

            vm.OnPageShown();

            await WaitUntilAsync(() => vm.RecentItems.Count == 1);
            Assert.Equal("Tools", vm.RecentItems[0].Title);
        }

        [Fact]
        public async Task OnPageShown_SkipsTheReadIfTheListIsFresh()
        {
            var vm = await CreateInitialisedViewModelAsync();
            vm.RecentItemsMaxAge = TimeSpan.FromHours(1);
            var reads = _recent.ReadCount;

            vm.OnPageShown();
            await Task.Delay(100);

            Assert.Equal(reads, _recent.ReadCount);
        }

        [Fact]
        public async Task RefreshRecentItems_WhenNothingChanged_KeepsTheSameItems()
        {
            _recent.Items.Add(new RecentItem(ToolsSln, LinkKind.Solution, DateTimeOffset.Now, false));
            var vm = await CreateInitialisedViewModelAsync();
            var before = Assert.Single(vm.RecentItems);

            await vm.RefreshRecentItemsAsync(CancellationToken.None);

            Assert.Same(before, Assert.Single(vm.RecentItems));
        }

        [Fact]
        public async Task RefreshRecentItems_WhenOverlapping_ShowsTheNewestResultOnlyOnce()
        {
            var vm = await CreateInitialisedViewModelAsync();
            var slowRead = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _recent.NextReadBlocker = slowRead.Task;
            _recent.Items.Add(new RecentItem(ToolsSln, LinkKind.Solution, DateTimeOffset.Now, false));

            var older = vm.RefreshRecentItemsAsync(CancellationToken.None);
            _recent.Items.Add(new RecentItem(HarnessSln, LinkKind.Solution, DateTimeOffset.Now, false));
            await vm.RefreshRecentItemsAsync(CancellationToken.None);
            slowRead.SetResult(true);
            await older;

            Assert.Equal(new[] { "Tools", "SS.Modelling.Harness" }, vm.RecentItems.Select(r => r.Title).ToArray());
        }

        [Fact]
        public async Task DiskChecks_NeverRunOnTheCallersThread()
        {
            _recent.Items.Add(new RecentItem(ToolsSln, LinkKind.Solution, DateTimeOffset.Now, false));
            var vm = await CreateInitialisedViewModelAsync();
            var checksBefore = _launcher.DiskChecks;

            await FakeUiContext.RunAsync(() => vm.OpenLinkAsync(vm.Sections[0].Links[0]));
            await FakeUiContext.RunAsync(() => vm.OpenRecentAsync(vm.RecentItems[0]));
            await FakeUiContext.RunAsync(() => vm.AddDroppedPathsAsync(vm.Sections[1], new[] { @"C:\Dev\My.Repo" }));

            Assert.Equal(checksBefore + 3, _launcher.DiskChecks);
            Assert.False(_launcher.CheckedOnCallersThread);
        }

        /// <summary>Simulates another instance editing the file (keeping ids), without the watcher noticing yet.</summary>
        private void ChangeOnDiskElsewhere(Action<StartPageLayout> edit)
        {
            var theirs = new StartPageLayout
            {
                Sections = _store.Layout.Sections.Select(s => new Section
                {
                    Id = s.Id,
                    Title = s.Title,
                    IsCollapsed = s.IsCollapsed,
                    Links = s.Links.Select(l => new LinkItem { Id = l.Id, Title = l.Title, Target = l.Target, Kind = l.Kind, Description = l.Description }).ToList(),
                }).ToList(),
            };
            edit(theirs);
            _store.Layout = theirs;
            _store.HasExternalChanges = true;
        }

        [Fact]
        public async Task SaveUnsavedChanges_WritesAnEditStillWaitingForTheSaveDelay()
        {
            var vm = await CreateInitialisedViewModelAsync();
            vm.SaveDelay = TimeSpan.FromHours(1);

            vm.Sections[0].Title = "Just before closing";
            Assert.True(vm.HasUnsavedChanges);
            await vm.SaveUnsavedChangesAsync(CancellationToken.None);

            Assert.Equal(1, _store.SaveCount);
            Assert.Equal("Just before closing", _store.LastSaved!.Sections[0].Title);
            Assert.False(vm.HasUnsavedChanges);
        }

        [Fact]
        public async Task SaveUnsavedChanges_WhenEverythingIsSaved_DoesNotWrite()
        {
            var vm = await CreateInitialisedViewModelAsync();
            vm.SaveDelay = TimeSpan.Zero;
            var save = _store.NextSave;
            vm.Sections[0].Title = "Saved already";
            await InMemoryLayoutStore.WithTimeout(save);
            await WaitUntilAsync(() => !vm.HasUnsavedChanges);

            await vm.SaveUnsavedChangesAsync(CancellationToken.None);

            Assert.Equal(1, _store.SaveCount);
        }

        [Fact]
        public async Task SaveUnsavedChanges_WaitsForASaveAlreadyUnderWay()
        {
            var vm = await CreateInitialisedViewModelAsync();
            var slowDisk = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _store.SaveBlocker = slowDisk.Task;
            vm.SaveDelay = TimeSpan.Zero;
            vm.Sections[0].Title = "Being written";
            await WaitUntilAsync(() => _store.SaveAttempts == 1);

            var shutdownSave = vm.SaveUnsavedChangesAsync(CancellationToken.None);
            await Task.Delay(100);
            Assert.False(shutdownSave.IsCompleted);

            slowDisk.SetResult(true);
            await shutdownSave;
            Assert.Equal(1, _store.SaveCount);
            Assert.Equal("Being written", _store.LastSaved!.Sections[0].Title);
        }

        [Fact]
        public async Task SaveUnsavedChanges_GivesUpWhenCancelled()
        {
            var vm = await CreateInitialisedViewModelAsync();
            _store.SaveBlocker = new TaskCompletionSource<bool>().Task; // never finishes
            vm.SaveDelay = TimeSpan.Zero;
            vm.Sections[0].Title = "Stuck";
            await WaitUntilAsync(() => _store.SaveAttempts == 1);

            using (var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(100)))
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => vm.SaveUnsavedChangesAsync(timeout.Token));
            }
        }

        private static StartPageLayout LayoutTitled(string title) =>
            new StartPageLayout { Sections = new List<Section> { new Section { Title = title } } };

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (!condition())
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new TimeoutException("The condition was never met.");
                }

                await Task.Delay(20);
            }
        }

        private async Task<StartPageViewModel> CreateInitialisedViewModelAsync()
        {
            var vm = CreateViewModel();
            await vm.InitializeAsync(CancellationToken.None);
            return vm;
        }

        private StartPageViewModel CreateViewModel() =>
            new StartPageViewModel(_store, _recent, _launcher, _shell, _dialogs, _settings, NullLogger<StartPageViewModel>.Instance);
    }
}

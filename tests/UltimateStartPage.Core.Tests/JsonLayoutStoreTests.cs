using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using UltimateStartPage.Core.Models;
using UltimateStartPage.Core.Services;
using Xunit;

namespace UltimateStartPage.Core.Tests
{
    public sealed class JsonLayoutStoreTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "usp-tests-" + Guid.NewGuid().ToString("N"));

        private readonly List<JsonLayoutStore> _stores = new List<JsonLayoutStore>();

        private string LayoutPath => Path.Combine(_directory, "layout.json");

        public void Dispose()
        {
            _stores.ForEach(s => s.Dispose());

            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        [Fact]
        public async Task LoadAsync_WhenFileMissing_ReturnsDefaultLayout()
        {
            var store = CreateStore();

            var layout = await store.LoadAsync(CancellationToken.None);

            Assert.Equal(DefaultLayout.Create().Sections.Select(s => s.Title), layout.Sections.Select(s => s.Title));
            Assert.False(File.Exists(LayoutPath));
        }

        [Fact]
        public async Task SaveAsync_ThenLoadAsync_RoundTripsSectionsAndLinks()
        {
            var store = CreateStore();
            var original = new StartPageLayout
            {
                Sections = new List<Section>
                {
                    new Section
                    {
                        Title = "Pricing",
                        IsCollapsed = true,
                        Links = new List<LinkItem>
                        {
                            new LinkItem { Title = "Harness", Target = @"C:\Dev\Harness\Harness.sln", Kind = LinkKind.Solution, Description = "Main" },
                            new LinkItem { Title = "Docs", Target = "https://learn.microsoft.com", Kind = LinkKind.Url },
                        },
                    },
                },
            };

            await store.SaveAsync(original, CancellationToken.None);
            var loaded = await store.LoadAsync(CancellationToken.None);

            var section = Assert.Single(loaded.Sections);
            Assert.Equal(original.Sections[0].Id, section.Id);
            Assert.Equal("Pricing", section.Title);
            Assert.True(section.IsCollapsed);
            Assert.Equal(2, section.Links.Count);
            Assert.Equal(original.Sections[0].Links[0].Id, section.Links[0].Id);
            Assert.Equal(LinkKind.Solution, section.Links[0].Kind);
            Assert.Equal("Main", section.Links[0].Description);
            Assert.Equal(LinkKind.Url, section.Links[1].Kind);
            Assert.Null(section.Links[1].Description);
        }

        [Fact]
        public async Task SaveAsync_WritesReadableJsonWithEnumNames()
        {
            var store = CreateStore();
            var layout = new StartPageLayout
            {
                Sections = new List<Section>
                {
                    new Section { Title = "A", Links = new List<LinkItem> { new LinkItem { Title = "x", Target = @"C:\x", Kind = LinkKind.Folder } } },
                },
            };

            await store.SaveAsync(layout, CancellationToken.None);

            var json = File.ReadAllText(LayoutPath);
            Assert.Contains("\"kind\": \"Folder\"", json);
            Assert.Contains("\"schemaVersion\": 1", json);
            Assert.False(File.Exists(LayoutPath + ".tmp"));
        }

        [Fact]
        public async Task SaveAsync_OverwritesExistingFile()
        {
            var store = CreateStore();
            await store.SaveAsync(new StartPageLayout { Sections = new List<Section> { new Section { Title = "First" } } }, CancellationToken.None);

            await store.SaveAsync(new StartPageLayout { Sections = new List<Section> { new Section { Title = "Second" } } }, CancellationToken.None);

            var loaded = await store.LoadAsync(CancellationToken.None);
            Assert.Equal("Second", Assert.Single(loaded.Sections).Title);
        }

        [Fact]
        public async Task LoadAsync_WhenFileCorrupt_MovesItAsideAndReturnsDefault()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(LayoutPath, "{ this is not json");
            var store = CreateStore();

            var layout = await store.LoadAsync(CancellationToken.None);

            Assert.Equal(DefaultLayout.Create().Sections.Count, layout.Sections.Count);
            Assert.False(File.Exists(LayoutPath));
            Assert.Single(Directory.GetFiles(_directory, "layout.json.corrupt-*"));
        }

        [Fact]
        public async Task LoadAsync_ToleratesCommentsTrailingCommasAndAnyCase()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(LayoutPath, @"{
                // hand edited
                ""SchemaVersion"": 1,
                ""sections"": [ { ""title"": ""Team"", ""links"": [ { ""target"": ""C:\\Dev\\Team\\Team.sln"", ""kind"": ""solution"" }, ] } ],
            }");
            var store = CreateStore();

            var layout = await store.LoadAsync(CancellationToken.None);

            var link = Assert.Single(Assert.Single(layout.Sections).Links);
            Assert.Equal("Team", link.Title);
            Assert.Equal(LinkKind.Solution, link.Kind);
            Assert.NotEqual(Guid.Empty, link.Id);
        }

        [Fact]
        public async Task ImportAsync_RejectsLayoutFromNewerSchema()
        {
            Directory.CreateDirectory(_directory);
            var path = Path.Combine(_directory, "future.json");
            File.WriteAllText(path, "{ \"schemaVersion\": 99, \"sections\": [] }");
            var store = CreateStore();

            var ex = await Assert.ThrowsAsync<LayoutFormatException>(() => store.ImportAsync(path, CancellationToken.None));

            Assert.Contains("newer version", ex.Message);
        }

        [Fact]
        public async Task ImportAsync_WhenFileMissing_Throws()
        {
            var store = CreateStore();

            await Assert.ThrowsAsync<FileNotFoundException>(() => store.ImportAsync(Path.Combine(_directory, "nope.json"), CancellationToken.None));
        }

        [Fact]
        public void Normalise_DropsEmptyLinksAndFixesDuplicateIds()
        {
            var sharedId = Guid.NewGuid();
            var layout = new StartPageLayout
            {
                Sections = new List<Section>
                {
                    new Section
                    {
                        Id = sharedId,
                        Title = "  ",
                        Links = new List<LinkItem>
                        {
                            new LinkItem { Id = sharedId, Title = "dup", Target = @"C:\a.sln" },
                            new LinkItem { Title = "no target", Target = "   " },
                            null!,
                        },
                    },
                    null!,
                },
            };

            var normalised = JsonLayoutStore.Normalise(layout);

            var section = Assert.Single(normalised.Sections);
            Assert.Equal("Untitled section", section.Title);
            var link = Assert.Single(section.Links);
            Assert.NotEqual(section.Id, link.Id);
        }

        [Fact]
        public async Task SaveAsync_WhenAnotherInstanceSavedSinceLoad_ThrowsAndKeepsTheirFile()
        {
            var mine = CreateStore();
            var theirs = CreateStore();
            await mine.LoadAsync(CancellationToken.None);
            await theirs.LoadAsync(CancellationToken.None);
            await theirs.SaveAsync(LayoutTitled("Theirs"), CancellationToken.None);

            await Assert.ThrowsAsync<LayoutConflictException>(() => mine.SaveAsync(LayoutTitled("Mine"), CancellationToken.None));

            Assert.Equal("Theirs", await ReadTitleFromDiskAsync());
        }

        [Fact]
        public async Task SaveAsync_AfterReloadingTheirChanges_Succeeds()
        {
            var mine = CreateStore();
            var theirs = CreateStore();
            await mine.LoadAsync(CancellationToken.None);
            await theirs.SaveAsync(LayoutTitled("Theirs"), CancellationToken.None);

            await mine.LoadAsync(CancellationToken.None);
            await mine.SaveAsync(LayoutTitled("Mine"), CancellationToken.None);

            Assert.Equal("Mine", await ReadTitleFromDiskAsync());
        }

        [Fact]
        public async Task SaveAsync_RepeatedSavesFromOneInstance_NeverConflict()
        {
            var store = CreateStore();
            await store.LoadAsync(CancellationToken.None);

            await store.SaveAsync(LayoutTitled("One"), CancellationToken.None);
            await store.SaveAsync(LayoutTitled("Two"), CancellationToken.None);

            Assert.False(await store.HasExternalChangesAsync(CancellationToken.None));
            Assert.Equal("Two", await ReadTitleFromDiskAsync());
        }

        [Fact]
        public async Task SaveAsync_WhenFileDeletedSinceLoad_RecreatesIt()
        {
            var store = CreateStore();
            await store.SaveAsync(LayoutTitled("One"), CancellationToken.None);
            File.Delete(LayoutPath);

            await store.SaveAsync(LayoutTitled("Two"), CancellationToken.None);

            Assert.Equal("Two", await ReadTitleFromDiskAsync());
        }

        [Fact]
        public async Task HasExternalChangesAsync_DetectsHandEdits()
        {
            var store = CreateStore();
            await store.SaveAsync(LayoutTitled("Saved"), CancellationToken.None);

            File.WriteAllText(LayoutPath, File.ReadAllText(LayoutPath).Replace("Saved", "Hand edited"));

            Assert.True(await store.HasExternalChangesAsync(CancellationToken.None));
        }

        [Fact]
        public async Task ExternalChange_IsRaisedForAnotherInstancesSave_ButNotForOwnSaves()
        {
            var mine = CreateStore(watch: true);
            var theirs = CreateStore();
            await mine.SaveAsync(LayoutTitled("Mine"), CancellationToken.None);
            var raised = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            mine.ExternalChange += (_, __) => raised.TrySetResult(true);

            await mine.SaveAsync(LayoutTitled("Mine again"), CancellationToken.None);
            await Task.Delay(500);
            Assert.False(raised.Task.IsCompleted, "A store's own save was reported as an outside change.");

            await theirs.LoadAsync(CancellationToken.None);
            await theirs.SaveAsync(LayoutTitled("Theirs"), CancellationToken.None);
            Assert.Same(raised.Task, await Task.WhenAny(raised.Task, Task.Delay(TimeSpan.FromSeconds(5))));
        }

        private static StartPageLayout LayoutTitled(string title) =>
            new StartPageLayout { Sections = new List<Section> { new Section { Title = title } } };

        private async Task<string> ReadTitleFromDiskAsync()
        {
            var layout = await CreateStore().LoadAsync(CancellationToken.None);
            return Assert.Single(layout.Sections).Title;
        }

        private JsonLayoutStore CreateStore(bool watch = false)
        {
            var options = new LayoutStoreOptions(LayoutPath)
            {
                WatchForExternalChanges = watch,
                ExternalChangeDelay = TimeSpan.FromMilliseconds(50),
            };
            var store = new JsonLayoutStore(options, NullLogger<JsonLayoutStore>.Instance);
            _stores.Add(store);
            return store;
        }
    }
}

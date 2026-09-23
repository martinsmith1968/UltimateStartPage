using System;
using System.Collections.Generic;
using System.Linq;
using UltimateStartPage.Core.Models;
using UltimateStartPage.Core.Services;
using Xunit;

namespace UltimateStartPage.Core.Tests
{
    public class LayoutMergerTests
    {
        private static readonly Guid Work = Guid.NewGuid();
        private static readonly Guid Personal = Guid.NewGuid();
        private static readonly Guid Harness = Guid.NewGuid();
        private static readonly Guid Tools = Guid.NewGuid();
        private static readonly Guid Blog = Guid.NewGuid();

        /// <summary>Work: Harness, Tools. Personal: Blog.</summary>
        private static StartPageLayout Ancestor() => new StartPageLayout
        {
            Sections = new List<Section>
            {
                new Section
                {
                    Id = Work,
                    Title = "Work",
                    Links = new List<LinkItem>
                    {
                        new LinkItem { Id = Harness, Title = "Harness", Target = @"C:\Dev\Harness\Harness.sln", Kind = LinkKind.Solution },
                        new LinkItem { Id = Tools, Title = "Tools", Target = @"C:\Dev\Tools\Tools.sln", Kind = LinkKind.Solution },
                    },
                },
                new Section
                {
                    Id = Personal,
                    Title = "Personal",
                    Links = new List<LinkItem>
                    {
                        new LinkItem { Id = Blog, Title = "Blog", Target = "https://example.com", Kind = LinkKind.Url },
                    },
                },
            },
        };

        [Fact]
        public void NothingChanged_ReturnsTheAncestor()
        {
            var result = LayoutMerger.Merge(Ancestor(), Ancestor(), Ancestor());

            Assert.True(LayoutMerger.AreEquivalent(Ancestor(), result.Layout));
            Assert.Empty(result.Conflicts);
        }

        [Fact]
        public void OnlyTheirsChanged_TakesTheirs()
        {
            var theirs = Edit(l => Section(l, Work).Title = "Day job");

            var result = LayoutMerger.Merge(Ancestor(), Ancestor(), theirs);

            Assert.True(LayoutMerger.AreEquivalent(theirs, result.Layout));
        }

        [Fact]
        public void DifferentFieldsChanged_CombinesBoth()
        {
            var mine = Edit(l => Section(l, Work).Title = "Day job");
            var theirs = Edit(l =>
            {
                Section(l, Work).IsCollapsed = true;
                Link(l, Harness).Description = "Main pricing solution";
            });

            var result = LayoutMerger.Merge(Ancestor(), mine, theirs);

            var work = Section(result.Layout, Work);
            Assert.Equal("Day job", work.Title);
            Assert.True(work.IsCollapsed);
            Assert.Equal("Main pricing solution", Link(result.Layout, Harness).Description);
            Assert.Empty(result.Conflicts);
        }

        [Fact]
        public void SameFieldChangedDifferently_MineWinsAndAConflictIsReported()
        {
            var mine = Edit(l => Link(l, Harness).Title = "Mine");
            var theirs = Edit(l => Link(l, Harness).Title = "Theirs");

            var result = LayoutMerger.Merge(Ancestor(), mine, theirs);

            Assert.Equal("Mine", Link(result.Layout, Harness).Title);
            Assert.Contains("title", Assert.Single(result.Conflicts));
        }

        [Fact]
        public void SameFieldChangedTheSameWay_IsNotAConflict()
        {
            var mine = Edit(l => Link(l, Harness).Title = "Same");
            var theirs = Edit(l => Link(l, Harness).Title = "Same");

            var result = LayoutMerger.Merge(Ancestor(), mine, theirs);

            Assert.Equal("Same", Link(result.Layout, Harness).Title);
            Assert.Empty(result.Conflicts);
        }

        [Fact]
        public void TargetAndKind_ChangeTogether()
        {
            var mine = Edit(l => Link(l, Harness).Description = "Now a folder");
            var theirs = Edit(l =>
            {
                Link(l, Harness).Target = @"C:\Dev\Harness";
                Link(l, Harness).Kind = LinkKind.Folder;
            });

            var result = LayoutMerger.Merge(Ancestor(), mine, theirs);

            var harness = Link(result.Layout, Harness);
            Assert.Equal(@"C:\Dev\Harness", harness.Target);
            Assert.Equal(LinkKind.Folder, harness.Kind);
            Assert.Equal("Now a folder", harness.Description);
        }

        [Fact]
        public void AddedOnBothSides_KeepsBoth()
        {
            var mineLink = Guid.NewGuid();
            var theirSection = Guid.NewGuid();
            var mine = Edit(l => Section(l, Work).Links.Add(new LinkItem { Id = mineLink, Title = "Mine", Target = @"C:\m.sln" }));
            var theirs = Edit(l => l.Sections.Add(new Section { Id = theirSection, Title = "Theirs" }));

            var result = LayoutMerger.Merge(Ancestor(), mine, theirs);

            Assert.Equal(new[] { Work, Personal, theirSection }, result.Layout.Sections.Select(s => s.Id));
            Assert.Equal(new[] { Harness, Tools, mineLink }, Section(result.Layout, Work).Links.Select(k => k.Id));
        }

        [Fact]
        public void DeletedThere_UnchangedHere_IsDeleted()
        {
            var theirs = Edit(l =>
            {
                Section(l, Work).Links.RemoveAll(k => k.Id == Tools);
                l.Sections.RemoveAll(s => s.Id == Personal);
            });

            var result = LayoutMerger.Merge(Ancestor(), Ancestor(), theirs);

            Assert.Equal(new[] { Work }, result.Layout.Sections.Select(s => s.Id));
            Assert.Equal(new[] { Harness }, Section(result.Layout, Work).Links.Select(k => k.Id));
            Assert.Empty(result.Conflicts);
        }

        [Fact]
        public void DeletedThere_ButEditedHere_IsKept()
        {
            var mine = Edit(l => Link(l, Tools).Title = "Tools (renamed)");
            var theirs = Edit(l => Section(l, Work).Links.RemoveAll(k => k.Id == Tools));

            var result = LayoutMerger.Merge(Ancestor(), mine, theirs);

            Assert.Equal("Tools (renamed)", Link(result.Layout, Tools).Title);
            Assert.Single(result.Conflicts);
        }

        [Fact]
        public void SectionDeletedThere_WhileALinkWasAddedToItHere_KeepsTheSectionWithOnlyTheNewLink()
        {
            var newLink = Guid.NewGuid();
            var mine = Edit(l => Section(l, Personal).Links.Add(new LinkItem { Id = newLink, Title = "New", Target = @"C:\n.sln" }));
            var theirs = Edit(l => l.Sections.RemoveAll(s => s.Id == Personal));

            var result = LayoutMerger.Merge(Ancestor(), mine, theirs);

            // Blog was deleted there along with the section and not touched here, so it goes; the new link stays.
            Assert.Equal(new[] { newLink }, Section(result.Layout, Personal).Links.Select(k => k.Id));
        }

        [Fact]
        public void MovedThere_EditedHere_MovesAndKeepsTheEdit()
        {
            var mine = Edit(l => Link(l, Tools).Description = "Build tools");
            var theirs = Edit(l =>
            {
                var tools = Link(l, Tools);
                Section(l, Work).Links.Remove(tools);
                Section(l, Personal).Links.Insert(0, tools);
            });

            var result = LayoutMerger.Merge(Ancestor(), mine, theirs);

            Assert.Equal(new[] { Harness }, Section(result.Layout, Work).Links.Select(k => k.Id));
            Assert.Equal(new[] { Tools, Blog }, Section(result.Layout, Personal).Links.Select(k => k.Id));
            Assert.Equal("Build tools", Link(result.Layout, Tools).Description);
        }

        [Fact]
        public void ReorderedThere_AddedHere_KeepsTheirOrderAndPlacesTheAddition()
        {
            var newSection = Guid.NewGuid();
            var mine = Edit(l => l.Sections.Insert(1, new Section { Id = newSection, Title = "Between" }));
            var theirs = Edit(l => l.Sections.Reverse());

            var result = LayoutMerger.Merge(Ancestor(), mine, theirs);

            // Mine put the new section after Work; theirs moved Work to the end.
            Assert.Equal(new[] { Personal, Work, newSection }, result.Layout.Sections.Select(s => s.Id));
        }

        [Fact]
        public void ReorderedOnBothSides_MineWins()
        {
            var mine = Edit(l => Section(l, Work).Links.Reverse());
            var theirs = Edit(l =>
            {
                var tools = Link(l, Tools);
                Section(l, Work).Links.Remove(tools);
                Section(l, Work).Links.Insert(0, tools);
                Section(l, Work).Links.Add(new LinkItem { Id = Guid.NewGuid(), Title = "Extra", Target = @"C:\x.sln" });
            });

            var result = LayoutMerger.Merge(Ancestor(), mine, theirs);

            var ids = Section(result.Layout, Work).Links.Select(k => k.Id).ToList();
            Assert.Equal(new[] { Tools, Harness }, ids.Take(2));
            Assert.Equal(3, ids.Count);
        }

        [Fact]
        public void Merge_DoesNotShareObjectsWithItsInputs()
        {
            var mine = Ancestor();

            var result = LayoutMerger.Merge(Ancestor(), mine, Ancestor());
            Section(result.Layout, Work).Title = "Changed";
            Link(result.Layout, Harness).Title = "Changed";

            Assert.Equal("Work", Section(mine, Work).Title);
            Assert.Equal("Harness", Link(mine, Harness).Title);
        }

        private static StartPageLayout Edit(Action<StartPageLayout> edit)
        {
            var layout = Ancestor();
            edit(layout);
            return layout;
        }

        private static Section Section(StartPageLayout layout, Guid id) => layout.Sections.Single(s => s.Id == id);

        private static LinkItem Link(StartPageLayout layout, Guid id) => layout.Sections.SelectMany(s => s.Links).Single(l => l.Id == id);
    }
}

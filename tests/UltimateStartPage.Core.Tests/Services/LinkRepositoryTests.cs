using System;
using System.Threading.Tasks;
using FluentAssertions;
using UltimateStartPage.Core.Models;
using UltimateStartPage.Core.Services;
using Xunit;

namespace UltimateStartPage.Core.Tests.Services
{
    public class LinkRepositoryTests
    {
        private readonly LinkRepository _sut = new LinkRepository();

        // --- GetGroupsAsync ---

        [Fact]
        public async Task GetGroupsAsync_InitiallyReturnsEmptyCollection()
        {
            var groups = await _sut.GetGroupsAsync();

            groups.Should().BeEmpty();
        }

        // --- AddLinkAsync ---

        [Fact]
        public async Task AddLinkAsync_CreatesGroupWhenItDoesNotExist()
        {
            var link = new SolutionLink("MySolution", @"C:\Projects\MySolution.sln");

            await _sut.AddLinkAsync("Work", link);

            var groups = await _sut.GetGroupsAsync();
            groups.Should().ContainSingle(g => g.Name == "Work");
        }

        [Fact]
        public async Task AddLinkAsync_AddsLinkToExistingGroup()
        {
            var link1 = new SolutionLink("A", @"C:\A.sln");
            var link2 = new SolutionLink("B", @"C:\B.sln");

            await _sut.AddLinkAsync("Work", link1);
            await _sut.AddLinkAsync("Work", link2);

            var groups = await _sut.GetGroupsAsync();
            groups.Should().ContainSingle();
            groups[0].Links.Should().HaveCount(2);
        }

        [Fact]
        public async Task AddLinkAsync_AllowsDuplicateFilePaths()
        {
            // LinkRepository does not de-duplicate — that is intentional;
            // the UI layer or a future validator owns that responsibility.
            var link1 = new SolutionLink("A", @"C:\Same.sln");
            var link2 = new SolutionLink("A copy", @"C:\Same.sln");

            await _sut.AddLinkAsync("Work", link1);
            await _sut.AddLinkAsync("Work", link2);

            var groups = await _sut.GetGroupsAsync();
            groups[0].Links.Should().HaveCount(2);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task AddLinkAsync_ThrowsOnNullOrWhitespaceGroupName(string badName)
        {
            var link = new SolutionLink("A", @"C:\A.sln");

            Func<Task> act = () => _sut.AddLinkAsync(badName, link);

            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task AddLinkAsync_ThrowsOnNullLink()
        {
            Func<Task> act = () => _sut.AddLinkAsync("Work", null);

            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        // --- RemoveLinkAsync ---

        [Fact]
        public async Task RemoveLinkAsync_RemovesMatchingLink()
        {
            var link = new SolutionLink("MySolution", @"C:\Projects\MySolution.sln");
            await _sut.AddLinkAsync("Work", link);

            await _sut.RemoveLinkAsync("Work", @"C:\Projects\MySolution.sln");

            var groups = await _sut.GetGroupsAsync();
            groups[0].Links.Should().BeEmpty();
        }

        [Fact]
        public async Task RemoveLinkAsync_DoesNotThrowWhenGroupDoesNotExist()
        {
            Func<Task> act = () => _sut.RemoveLinkAsync("NonExistent", @"C:\X.sln");

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task RemoveLinkAsync_DoesNotThrowWhenFilePathNotFound()
        {
            await _sut.AddLinkAsync("Work", new SolutionLink("A", @"C:\A.sln"));

            Func<Task> act = () => _sut.RemoveLinkAsync("Work", @"C:\DoesNotExist.sln");

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task RemoveLinkAsync_OnlyRemovesFirstMatchWhenDuplicatesExist()
        {
            await _sut.AddLinkAsync("Work", new SolutionLink("A", @"C:\Same.sln"));
            await _sut.AddLinkAsync("Work", new SolutionLink("B", @"C:\Same.sln"));

            await _sut.RemoveLinkAsync("Work", @"C:\Same.sln");

            var groups = await _sut.GetGroupsAsync();
            groups[0].Links.Should().HaveCount(1);
        }

        // --- SaveGroupsAsync / round-trip ---

        [Fact]
        public async Task SaveGroupsAsync_ReplacesExistingGroups()
        {
            await _sut.AddLinkAsync("OldGroup", new SolutionLink("Old", @"C:\Old.sln"));

            var newGroups = new[]
            {
                new LinkGroup("NewGroup")
            };
            newGroups[0].Links.Add(new SolutionLink("New", @"C:\New.sln"));

            await _sut.SaveGroupsAsync(newGroups);

            var groups = await _sut.GetGroupsAsync();
            groups.Should().ContainSingle(g => g.Name == "NewGroup");
            groups[0].Links.Should().ContainSingle(l => l.FilePath == @"C:\New.sln");
        }

        [Fact]
        public async Task SaveGroupsAsync_AcceptsEmptyList()
        {
            await _sut.AddLinkAsync("Work", new SolutionLink("A", @"C:\A.sln"));

            await _sut.SaveGroupsAsync(Array.Empty<LinkGroup>());

            var groups = await _sut.GetGroupsAsync();
            groups.Should().BeEmpty();
        }
    }
}

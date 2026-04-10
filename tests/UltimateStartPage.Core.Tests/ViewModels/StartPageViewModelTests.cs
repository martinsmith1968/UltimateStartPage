using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using UltimateStartPage.Core.Models;
using UltimateStartPage.Core.Services;
using UltimateStartPage.Core.ViewModels;
using Xunit;

namespace UltimateStartPage.Core.Tests.ViewModels
{
    public class StartPageViewModelTests
    {
        private readonly ILinkRepository _repository;
        private readonly StartPageViewModel _sut;

        public StartPageViewModelTests()
        {
            _repository = Substitute.For<ILinkRepository>();
            _repository.GetGroupsAsync().Returns(Task.FromResult<IReadOnlyList<LinkGroup>>(new List<LinkGroup>()));
            _repository.SaveGroupsAsync(Arg.Any<IReadOnlyList<LinkGroup>>()).Returns(Task.CompletedTask);
            _sut = new StartPageViewModel(_repository);
        }

        [Fact]
        public async Task LoadAsync_PopulatesGroupsFromRepository()
        {
            var groups = new List<LinkGroup>
            {
                new LinkGroup("Work"),
                new LinkGroup("Personal"),
            };
            _repository.GetGroupsAsync().Returns(Task.FromResult<IReadOnlyList<LinkGroup>>(groups));

            await _sut.LoadAsync();

            _sut.Groups.Should().HaveCount(2);
            _sut.Groups[0].Name.Should().Be("Work");
            _sut.Groups[1].Name.Should().Be("Personal");
        }

        [Fact]
        public async Task LoadAsync_SetsHasGroupsTrue_WhenRepositoryHasGroups()
        {
            var groups = new List<LinkGroup> { new LinkGroup("Work") };
            _repository.GetGroupsAsync().Returns(Task.FromResult<IReadOnlyList<LinkGroup>>(groups));

            await _sut.LoadAsync();

            _sut.HasGroups.Should().BeTrue();
        }

        [Fact]
        public async Task LoadAsync_SetsHasGroupsFalse_WhenRepositoryIsEmpty()
        {
            _repository.GetGroupsAsync().Returns(
                Task.FromResult<IReadOnlyList<LinkGroup>>(new List<LinkGroup>()));

            await _sut.LoadAsync();

            _sut.HasGroups.Should().BeFalse();
        }

        [Fact]
        public async Task LoadAsync_PopulatesLinksWithinGroups()
        {
            var group = new LinkGroup("Work");
            group.Links.Add(new SolutionLink("MySolution", @"C:\Projects\MySolution.sln"));
            _repository.GetGroupsAsync().Returns(
                Task.FromResult<IReadOnlyList<LinkGroup>>(new List<LinkGroup> { group }));

            await _sut.LoadAsync();

            _sut.Groups[0].Links.Should().ContainSingle(l => l.Name == "MySolution");
        }

        [Fact]
        public void AddGroupCommand_AddsGroupToCollection()
        {
            _sut.Groups.Should().BeEmpty();

            _sut.AddGroupCommand.Execute(null);

            // Give async void a moment to complete.
            // AddGroupCommand is AsyncRelayCommand — fires and saves asynchronously.
            // We verify the group is added synchronously on the collection first.
            _sut.Groups.Should().HaveCount(1);
        }

        [Fact]
        public void AddGroupCommand_SetsHasGroupsTrue()
        {
            _sut.HasGroups.Should().BeFalse();

            _sut.AddGroupCommand.Execute(null);

            _sut.HasGroups.Should().BeTrue();
        }

        [Fact]
        public async Task LoadAsync_ClearsExistingGroupsBeforeReload()
        {
            // First load
            var firstGroups = new List<LinkGroup> { new LinkGroup("OldGroup") };
            _repository.GetGroupsAsync().Returns(Task.FromResult<IReadOnlyList<LinkGroup>>(firstGroups));
            await _sut.LoadAsync();
            _sut.Groups.Should().HaveCount(1);

            // Second load with different data
            var secondGroups = new List<LinkGroup> { new LinkGroup("A"), new LinkGroup("B") };
            _repository.GetGroupsAsync().Returns(Task.FromResult<IReadOnlyList<LinkGroup>>(secondGroups));
            await _sut.LoadAsync();

            _sut.Groups.Should().HaveCount(2);
            _sut.Groups.Should().NotContain(g => g.Name == "OldGroup");
        }
    }
}

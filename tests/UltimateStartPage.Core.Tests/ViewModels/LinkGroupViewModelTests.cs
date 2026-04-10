using System;
using System.Threading.Tasks;
using FluentAssertions;
using UltimateStartPage.Core.Models;
using UltimateStartPage.Core.ViewModels;
using Xunit;

namespace UltimateStartPage.Core.Tests.ViewModels
{
    public class LinkGroupViewModelTests
    {
        [Fact]
        public void Constructor_SetsNameFromModel()
        {
            var model = new LinkGroup("Work Projects");

            var sut = new LinkGroupViewModel(model, _ => Task.CompletedTask, () => Task.CompletedTask);

            sut.Name.Should().Be("Work Projects");
        }

        [Fact]
        public void Constructor_WithNullModel_SetsNameToEmptyString()
        {
            var sut = new LinkGroupViewModel(null, _ => Task.CompletedTask, () => Task.CompletedTask);

            sut.Name.Should().BeEmpty();
        }

        [Fact]
        public void Constructor_PopulatesLinksFromModel()
        {
            var model = new LinkGroup("Work");
            model.Links.Add(new SolutionLink("Solution1", @"C:\Path\Solution1.sln"));
            model.Links.Add(new SolutionLink("Solution2", @"C:\Path\Solution2.sln"));

            var sut = new LinkGroupViewModel(model, _ => Task.CompletedTask, () => Task.CompletedTask);

            sut.Links.Should().HaveCount(2);
            sut.Links[0].Name.Should().Be("Solution1");
            sut.Links[0].Path.Should().Be(@"C:\Path\Solution1.sln");
            sut.Links[1].Name.Should().Be("Solution2");
            sut.Links[1].Path.Should().Be(@"C:\Path\Solution2.sln");
        }

        [Fact]
        public void Constructor_WithEmptyGroup_ProducesEmptyLinksCollection()
        {
            var model = new LinkGroup("EmptyGroup");

            var sut = new LinkGroupViewModel(model, _ => Task.CompletedTask, () => Task.CompletedTask);

            sut.Links.Should().BeEmpty();
        }

        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenOnRemoveIsNull()
        {
            var model = new LinkGroup("Test");

            Action act = () => new LinkGroupViewModel(model, null, () => Task.CompletedTask);

            act.Should().Throw<ArgumentNullException>().WithParameterName("onRemove");
        }

        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenSaveCallbackIsNull()
        {
            var model = new LinkGroup("Test");

            Action act = () => new LinkGroupViewModel(model, _ => Task.CompletedTask, null);

            act.Should().Throw<ArgumentNullException>().WithParameterName("saveCallback");
        }

        [Fact]
        public async Task AddLinkCommand_AddsLinkToLinksCollection()
        {
            var model = new LinkGroup("Test");
            var sut = new LinkGroupViewModel(model, _ => Task.CompletedTask, () => Task.CompletedTask);

            sut.Links.Should().BeEmpty();

            sut.AddLinkCommand.Execute(null);
            await Task.Delay(50); // Give async command time to complete

            sut.Links.Should().HaveCount(1);
            sut.Links[0].Name.Should().Be("New Link");
        }

        [Fact]
        public async Task AddLinkCommand_CallsSaveCallback()
        {
            var model = new LinkGroup("Test");
            var saveCalled = false;
            Task SaveCallback()
            {
                saveCalled = true;
                return Task.CompletedTask;
            }

            var sut = new LinkGroupViewModel(model, _ => Task.CompletedTask, SaveCallback);

            sut.AddLinkCommand.Execute(null);
            await Task.Delay(50); // Give async command time to complete

            saveCalled.Should().BeTrue();
        }

        [Fact]
        public void RemoveLink_RemovesCorrectLinkFromLinks()
        {
            var model = new LinkGroup("Test");
            model.Links.Add(new SolutionLink("Link1", @"C:\Path1.sln"));
            model.Links.Add(new SolutionLink("Link2", @"C:\Path2.sln"));
            model.Links.Add(new SolutionLink("Link3", @"C:\Path3.sln"));

            var sut = new LinkGroupViewModel(model, _ => Task.CompletedTask, () => Task.CompletedTask);
            var linkToRemove = sut.Links[1];

            linkToRemove.RemoveCommand.Execute(null);

            sut.Links.Should().HaveCount(2);
            sut.Links.Should().NotContain(linkToRemove);
            sut.Links[0].Name.Should().Be("Link1");
            sut.Links[1].Name.Should().Be("Link3");
        }

        [Fact]
        public async Task RemoveLink_CallsSaveCallback()
        {
            var model = new LinkGroup("Test");
            model.Links.Add(new SolutionLink("Link1", @"C:\Path1.sln"));

            var saveCalled = false;
            Task SaveCallback()
            {
                saveCalled = true;
                return Task.CompletedTask;
            }

            var sut = new LinkGroupViewModel(model, _ => Task.CompletedTask, SaveCallback);
            var linkToRemove = sut.Links[0];

            linkToRemove.RemoveCommand.Execute(null);
            await Task.Delay(50); // Give async callback time to complete

            saveCalled.Should().BeTrue();
        }

        [Fact]
        public void Name_Setter_RaisesPropertyChanged()
        {
            var model = new LinkGroup("Original");
            var sut = new LinkGroupViewModel(model, _ => Task.CompletedTask, () => Task.CompletedTask);
            var eventRaised = false;
            sut.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(LinkGroupViewModel.Name))
                    eventRaised = true;
            };

            sut.Name = "Updated Name";

            eventRaised.Should().BeTrue();
            sut.Name.Should().Be("Updated Name");
        }

        [Fact]
        public void ToModel_ReturnsLinkGroupWithCurrentState()
        {
            var model = new LinkGroup("Original");
            model.Links.Add(new SolutionLink("Link1", @"C:\Path1.sln"));

            var sut = new LinkGroupViewModel(model, _ => Task.CompletedTask, () => Task.CompletedTask);
            sut.Name = "Modified Group";

            var result = sut.ToModel();

            result.Name.Should().Be("Modified Group");
            result.Links.Should().HaveCount(1);
            result.Links[0].Name.Should().Be("Link1");
            result.Links[0].FilePath.Should().Be(@"C:\Path1.sln");
        }

        [Fact]
        public void RemoveCommand_InvokesOnRemoveCallback()
        {
            var model = new LinkGroup("Test");
            LinkGroupViewModel removedVm = null;
            Task OnRemove(LinkGroupViewModel vm)
            {
                removedVm = vm;
                return Task.CompletedTask;
            }

            var sut = new LinkGroupViewModel(model, OnRemove, () => Task.CompletedTask);

            sut.RemoveCommand.Execute(null);

            removedVm.Should().Be(sut);
        }
    }
}

using System;
using System.Threading.Tasks;
using FluentAssertions;
using UltimateStartPage.Core.Models;
using UltimateStartPage.Core.ViewModels;
using Xunit;

namespace UltimateStartPage.Core.Tests.ViewModels
{
    public class LinkViewModelTests
    {
        [Fact]
        public void Constructor_SetsNameFromModel()
        {
            var model = new SolutionLink("MySolution", @"C:\Projects\MySolution.sln");

            var sut = new LinkViewModel(model, _ => Task.CompletedTask);

            sut.Name.Should().Be("MySolution");
        }

        [Fact]
        public void Constructor_SetsPathFromModel()
        {
            var model = new SolutionLink("MySolution", @"C:\Projects\MySolution.sln");

            var sut = new LinkViewModel(model, _ => Task.CompletedTask);

            sut.Path.Should().Be(@"C:\Projects\MySolution.sln");
        }

        [Fact]
        public void Constructor_WithNullModel_SetsNameAndPathToEmptyString()
        {
            var sut = new LinkViewModel(null, _ => Task.CompletedTask);

            sut.Name.Should().BeEmpty();
            sut.Path.Should().BeEmpty();
        }

        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenOnRemoveIsNull()
        {
            var model = new SolutionLink("Test", @"C:\Test.sln");

            Action act = () => new LinkViewModel(model, null);

            act.Should().Throw<ArgumentNullException>().WithParameterName("onRemove");
        }

        [Fact]
        public void OpenCommand_CanBeInvoked_WhenPathIsNotEmpty()
        {
            var model = new SolutionLink("Test", @"C:\Test.sln");
            var sut = new LinkViewModel(model, _ => Task.CompletedTask);

            sut.OpenCommand.CanExecute(null).Should().BeTrue();
        }

        [Fact]
        public void OpenCommand_CannotBeInvoked_WhenPathIsEmpty()
        {
            var model = new SolutionLink("Test", string.Empty);
            var sut = new LinkViewModel(model, _ => Task.CompletedTask);

            sut.OpenCommand.CanExecute(null).Should().BeFalse();
        }

        [Fact]
        public void OpenCommand_CannotBeInvoked_WhenPathIsWhitespace()
        {
            var model = new SolutionLink("Test", "   ");
            var sut = new LinkViewModel(model, _ => Task.CompletedTask);

            sut.OpenCommand.CanExecute(null).Should().BeFalse();
        }

        [Fact]
        public void Path_IsNeverNull_WhenConstructedFromValidSolutionLink()
        {
            var model = new SolutionLink("Test", @"C:\Test.sln");
            var sut = new LinkViewModel(model, _ => Task.CompletedTask);

            sut.Path.Should().NotBeNull();
        }

        [Fact]
        public void Name_Setter_RaisesPropertyChanged()
        {
            var model = new SolutionLink("Original", @"C:\Test.sln");
            var sut = new LinkViewModel(model, _ => Task.CompletedTask);
            var eventRaised = false;
            sut.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(LinkViewModel.Name))
                    eventRaised = true;
            };

            sut.Name = "Updated Name";

            eventRaised.Should().BeTrue();
            sut.Name.Should().Be("Updated Name");
        }

        [Fact]
        public void Path_Setter_RaisesPropertyChanged()
        {
            var model = new SolutionLink("Test", @"C:\Original.sln");
            var sut = new LinkViewModel(model, _ => Task.CompletedTask);
            var eventRaised = false;
            sut.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(LinkViewModel.Path))
                    eventRaised = true;
            };

            sut.Path = @"C:\Updated.sln";

            eventRaised.Should().BeTrue();
            sut.Path.Should().Be(@"C:\Updated.sln");
        }

        [Fact]
        public void Path_Setter_RaisesCanExecuteChangedOnOpenCommand()
        {
            var model = new SolutionLink("Test", string.Empty);
            var sut = new LinkViewModel(model, _ => Task.CompletedTask);
            var canExecuteChangedRaised = false;
            sut.OpenCommand.CanExecuteChanged += (s, e) => canExecuteChangedRaised = true;

            sut.OpenCommand.CanExecute(null).Should().BeFalse();

            sut.Path = @"C:\Test.sln";

            canExecuteChangedRaised.Should().BeTrue();
            sut.OpenCommand.CanExecute(null).Should().BeTrue();
        }

        [Fact]
        public void ToModel_ReturnsSolutionLinkWithCurrentState()
        {
            var model = new SolutionLink("Original", @"C:\Original.sln");
            var sut = new LinkViewModel(model, _ => Task.CompletedTask);
            sut.Name = "Modified Name";
            sut.Path = @"C:\Modified.sln";

            var result = sut.ToModel();

            result.Name.Should().Be("Modified Name");
            result.FilePath.Should().Be(@"C:\Modified.sln");
        }

        [Fact]
        public void RemoveCommand_InvokesOnRemoveCallback()
        {
            var model = new SolutionLink("Test", @"C:\Test.sln");
            LinkViewModel removedVm = null;
            Task OnRemove(LinkViewModel vm)
            {
                removedVm = vm;
                return Task.CompletedTask;
            }

            var sut = new LinkViewModel(model, OnRemove);

            sut.RemoveCommand.Execute(null);

            removedVm.Should().Be(sut);
        }

        [Fact]
        public void OpenCommand_IsWiredUp()
        {
            var model = new SolutionLink("Test", @"C:\Test.sln");
            var sut = new LinkViewModel(model, _ => Task.CompletedTask);

            sut.OpenCommand.Should().NotBeNull();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_WithEmptyOrWhitespacePath_CreatesOpenCommandInDisabledState(string path)
        {
            var model = new SolutionLink("Test", path);
            var sut = new LinkViewModel(model, _ => Task.CompletedTask);

            sut.OpenCommand.CanExecute(null).Should().BeFalse();
        }
    }
}

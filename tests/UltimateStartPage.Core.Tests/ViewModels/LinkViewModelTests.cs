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

            var sut = new LinkViewModel(model, _ => Task.CompletedTask, () => Task.CompletedTask);

            sut.Name.Should().Be("MySolution");
        }

        [Fact]
        public void Constructor_SetsPathFromModel()
        {
            var model = new SolutionLink("MySolution", @"C:\Projects\MySolution.sln");

            var sut = new LinkViewModel(model, _ => Task.CompletedTask, () => Task.CompletedTask);

            sut.Path.Should().Be(@"C:\Projects\MySolution.sln");
        }

        [Fact]
        public void Constructor_WithNullModel_SetsNameAndPathToEmptyString()
        {
            var sut = new LinkViewModel(null, _ => Task.CompletedTask, () => Task.CompletedTask);

            sut.Name.Should().BeEmpty();
            sut.Path.Should().BeEmpty();
        }

        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenOnRemoveIsNull()
        {
            var model = new SolutionLink("Test", @"C:\Test.sln");

            Action act = () => new LinkViewModel(model, null, () => Task.CompletedTask);

            act.Should().Throw<ArgumentNullException>().WithParameterName("onRemove");
        }

        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenSaveCallbackIsNull()
        {
            var model = new SolutionLink("Test", @"C:\Test.sln");

            Action act = () => new LinkViewModel(model, _ => Task.CompletedTask, null);

            act.Should().Throw<ArgumentNullException>().WithParameterName("saveCallback");
        }

        [Fact]
        public void OpenCommand_CanBeInvoked_WhenPathIsNotEmpty()
        {
            var model = new SolutionLink("Test", @"C:\Test.sln");
            var sut = new LinkViewModel(model, _ => Task.CompletedTask, () => Task.CompletedTask);

            sut.OpenCommand.CanExecute(null).Should().BeTrue();
        }

        [Fact]
        public void OpenCommand_CannotBeInvoked_WhenPathIsEmpty()
        {
            var model = new SolutionLink("Test", string.Empty);
            var sut = new LinkViewModel(model, _ => Task.CompletedTask, () => Task.CompletedTask);

            sut.OpenCommand.CanExecute(null).Should().BeFalse();
        }

        [Fact]
        public void OpenCommand_CannotBeInvoked_WhenPathIsWhitespace()
        {
            var model = new SolutionLink("Test", "   ");
            var sut = new LinkViewModel(model, _ => Task.CompletedTask, () => Task.CompletedTask);

            sut.OpenCommand.CanExecute(null).Should().BeFalse();
        }

        [Fact]
        public void Path_IsNeverNull_WhenConstructedFromValidSolutionLink()
        {
            var model = new SolutionLink("Test", @"C:\Test.sln");
            var sut = new LinkViewModel(model, _ => Task.CompletedTask, () => Task.CompletedTask);

            sut.Path.Should().NotBeNull();
        }

        [Fact]
        public void Name_Setter_RaisesPropertyChanged()
        {
            var model = new SolutionLink("Original", @"C:\Test.sln");
            var sut = new LinkViewModel(model, _ => Task.CompletedTask, () => Task.CompletedTask);
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
            var sut = new LinkViewModel(model, _ => Task.CompletedTask, () => Task.CompletedTask);
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
            var sut = new LinkViewModel(model, _ => Task.CompletedTask, () => Task.CompletedTask);
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
            var sut = new LinkViewModel(model, _ => Task.CompletedTask, () => Task.CompletedTask);
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

            var sut = new LinkViewModel(model, OnRemove, () => Task.CompletedTask);

            sut.RemoveCommand.Execute(null);

            removedVm.Should().Be(sut);
        }

        [Fact]
        public void OpenCommand_IsWiredUp()
        {
            var model = new SolutionLink("Test", @"C:\Test.sln");
            var sut = new LinkViewModel(model, _ => Task.CompletedTask, () => Task.CompletedTask);

            sut.OpenCommand.Should().NotBeNull();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_WithEmptyOrWhitespacePath_CreatesOpenCommandInDisabledState(string path)
        {
            var model = new SolutionLink("Test", path);
            var sut = new LinkViewModel(model, _ => Task.CompletedTask, () => Task.CompletedTask);

            sut.OpenCommand.CanExecute(null).Should().BeFalse();
        }

        // anticipatory — requires McManus CRUD commands
        [Fact]
        public void BeginEditCommand_SetsIsEditingTrue()
        {
            var model = new SolutionLink("Original Name", @"C:\Original.sln");
            var sut = new LinkViewModel(model, _ => Task.CompletedTask, () => Task.CompletedTask);

            sut.BeginEditCommand.Execute(null);

            sut.IsEditing.Should().BeTrue();
        }

        // anticipatory — requires McManus CRUD commands
        [Fact]
        public void BeginEditCommand_CopiesNameToEditingName()
        {
            var model = new SolutionLink("Original Name", @"C:\Original.sln");
            var sut = new LinkViewModel(model, _ => Task.CompletedTask, () => Task.CompletedTask);

            sut.BeginEditCommand.Execute(null);

            sut.EditingName.Should().Be("Original Name");
        }

        // anticipatory — requires McManus CRUD commands
        [Fact]
        public void BeginEditCommand_CopiesPathToEditingPath()
        {
            var model = new SolutionLink("Original Name", @"C:\Original.sln");
            var sut = new LinkViewModel(model, _ => Task.CompletedTask, () => Task.CompletedTask);

            sut.BeginEditCommand.Execute(null);

            sut.EditingPath.Should().Be(@"C:\Original.sln");
        }

        // anticipatory — requires McManus CRUD commands
        [Fact]
        public async Task CommitEditCommand_SetsIsEditingFalse()
        {
            var model = new SolutionLink("Original", @"C:\Original.sln");
            var sut = new LinkViewModel(model, _ => Task.CompletedTask, () => Task.CompletedTask);
            sut.BeginEditCommand.Execute(null);
            sut.EditingName = "Updated Name";
            sut.EditingPath = @"C:\Updated.sln";

            sut.CommitEditCommand.Execute(null);
            await Task.Delay(50); // Give async command time to complete

            sut.IsEditing.Should().BeFalse();
        }

        // anticipatory — requires McManus CRUD commands
        [Fact]
        public async Task CommitEditCommand_AppliesEditingNameToName()
        {
            var model = new SolutionLink("Original", @"C:\Original.sln");
            var sut = new LinkViewModel(model, _ => Task.CompletedTask, () => Task.CompletedTask);
            sut.BeginEditCommand.Execute(null);
            sut.EditingName = "Updated Name";

            sut.CommitEditCommand.Execute(null);
            await Task.Delay(50); // Give async command time to complete

            sut.Name.Should().Be("Updated Name");
        }

        // anticipatory — requires McManus CRUD commands
        [Fact]
        public async Task CommitEditCommand_AppliesEditingPathToPath()
        {
            var model = new SolutionLink("Original", @"C:\Original.sln");
            var sut = new LinkViewModel(model, _ => Task.CompletedTask, () => Task.CompletedTask);
            sut.BeginEditCommand.Execute(null);
            sut.EditingPath = @"C:\Updated.sln";

            sut.CommitEditCommand.Execute(null);
            await Task.Delay(50); // Give async command time to complete

            sut.Path.Should().Be(@"C:\Updated.sln");
        }

        // anticipatory — requires McManus CRUD commands
        [Fact]
        public async Task CommitEditCommand_TriggersSaveCallbackViaPropertySetters()
        {
            var model = new SolutionLink("Original", @"C:\Original.sln");
            var saveCallCount = 0;
            Task SaveCallback()
            {
                saveCallCount++;
                return Task.CompletedTask;
            }

            var sut = new LinkViewModel(model, _ => Task.CompletedTask, SaveCallback);
            sut.BeginEditCommand.Execute(null);
            sut.EditingName = "Updated";
            sut.EditingPath = @"C:\Updated.sln";

            sut.CommitEditCommand.Execute(null);
            await Task.Delay(50); // Give async command time to complete

            // CommitEdit sets Name and Path, each triggering save callback (2 times)
            saveCallCount.Should().BeGreaterThan(0);
        }

        // anticipatory — requires McManus CRUD commands
        [Fact]
        public void CancelEditCommand_SetsIsEditingFalse()
        {
            var model = new SolutionLink("Original", @"C:\Original.sln");
            var sut = new LinkViewModel(model, _ => Task.CompletedTask, () => Task.CompletedTask);
            sut.BeginEditCommand.Execute(null);
            sut.EditingName = "Modified but will be discarded";
            sut.EditingPath = @"C:\Discarded.sln";

            sut.CancelEditCommand.Execute(null);

            sut.IsEditing.Should().BeFalse();
        }

        // anticipatory — requires McManus CRUD commands
        [Fact]
        public void CancelEditCommand_DoesNotChangeName()
        {
            var model = new SolutionLink("Original", @"C:\Original.sln");
            var sut = new LinkViewModel(model, _ => Task.CompletedTask, () => Task.CompletedTask);
            sut.BeginEditCommand.Execute(null);
            sut.EditingName = "Modified but will be discarded";

            sut.CancelEditCommand.Execute(null);

            sut.Name.Should().Be("Original");
        }

        // anticipatory — requires McManus CRUD commands
        [Fact]
        public void CancelEditCommand_DoesNotChangePath()
        {
            var model = new SolutionLink("Original", @"C:\Original.sln");
            var sut = new LinkViewModel(model, _ => Task.CompletedTask, () => Task.CompletedTask);
            sut.BeginEditCommand.Execute(null);
            sut.EditingPath = @"C:\Discarded.sln";

            sut.CancelEditCommand.Execute(null);

            sut.Path.Should().Be(@"C:\Original.sln");
        }

        // anticipatory — requires McManus CRUD commands
        [Fact]
        public void IsEditing_Setter_RaisesPropertyChanged()
        {
            var model = new SolutionLink("Test", @"C:\Test.sln");
            var sut = new LinkViewModel(model, _ => Task.CompletedTask, () => Task.CompletedTask);
            var eventRaised = false;
            sut.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(LinkViewModel.IsEditing))
                    eventRaised = true;
            };

            sut.BeginEditCommand.Execute(null);

            eventRaised.Should().BeTrue();
        }

        // anticipatory — requires McManus CRUD commands
        [Fact]
        public void EditingName_Setter_RaisesPropertyChanged()
        {
            var model = new SolutionLink("Test", @"C:\Test.sln");
            var sut = new LinkViewModel(model, _ => Task.CompletedTask, () => Task.CompletedTask);
            sut.BeginEditCommand.Execute(null);
            var eventRaised = false;
            sut.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(LinkViewModel.EditingName))
                    eventRaised = true;
            };

            sut.EditingName = "New Value";

            eventRaised.Should().BeTrue();
        }

        // anticipatory — requires McManus CRUD commands
        [Fact]
        public void EditingPath_Setter_RaisesPropertyChanged()
        {
            var model = new SolutionLink("Test", @"C:\Test.sln");
            var sut = new LinkViewModel(model, _ => Task.CompletedTask, () => Task.CompletedTask);
            sut.BeginEditCommand.Execute(null);
            var eventRaised = false;
            sut.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(LinkViewModel.EditingPath))
                    eventRaised = true;
            };

            sut.EditingPath = @"C:\NewPath.sln";

            eventRaised.Should().BeTrue();
        }
    }
}

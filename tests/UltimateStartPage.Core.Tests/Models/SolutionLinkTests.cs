using System;
using FluentAssertions;
using UltimateStartPage.Core.Models;
using Xunit;

namespace UltimateStartPage.Core.Tests.Models
{
    public class SolutionLinkTests
    {
        [Fact]
        public void Constructor_SetsNameAndFilePath()
        {
            var link = new SolutionLink("My Solution", @"C:\Projects\MySolution.sln");

            link.Name.Should().Be("My Solution");
            link.FilePath.Should().Be(@"C:\Projects\MySolution.sln");
        }

        [Fact]
        public void Constructor_ThrowsOnNullName()
        {
            Action act = () => new SolutionLink(null, @"C:\Projects\MySolution.sln");

            act.Should().Throw<ArgumentNullException>().WithParameterName("name");
        }

        [Fact]
        public void Constructor_ThrowsOnNullFilePath()
        {
            Action act = () => new SolutionLink("My Solution", null);

            act.Should().Throw<ArgumentNullException>().WithParameterName("filePath");
        }

        [Fact]
        public void DefaultConstructor_LeavesPropertiesAsDefault()
        {
            var link = new SolutionLink();

            link.Name.Should().BeNull();
            link.FilePath.Should().BeNull();
        }
    }
}

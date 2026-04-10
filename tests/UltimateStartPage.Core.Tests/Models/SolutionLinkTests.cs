using System;
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

            Assert.Equal("My Solution", link.Name);
            Assert.Equal(@"C:\Projects\MySolution.sln", link.FilePath);
        }

        [Fact]
        public void Constructor_ThrowsOnNullName()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SolutionLink(null, @"C:\Projects\MySolution.sln"));
        }

        [Fact]
        public void Constructor_ThrowsOnNullFilePath()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SolutionLink("My Solution", null));
        }

        [Fact]
        public void DefaultConstructor_LeavesPropertiesAsDefault()
        {
            var link = new SolutionLink();

            Assert.Null(link.Name);
            Assert.Null(link.FilePath);
        }
    }
}

using System;
using UltimateStartPage.Core.Models;
using UltimateStartPage.Core.Services;
using Xunit;

namespace UltimateStartPage.Core.Tests
{
    public class LinkKindDetectorTests
    {
        [Theory]
        [InlineData(@"C:\Dev\Pricing\Pricing.sln", LinkKind.Solution)]
        [InlineData(@"C:\Dev\Pricing\Pricing.slnx", LinkKind.Solution)]
        [InlineData(@"C:\Dev\Pricing\Pricing.Api\Pricing.Api.csproj", LinkKind.Project)]
        [InlineData(@"C:\Dev\Pricing\Model\Model.fsproj", LinkKind.Project)]
        [InlineData(@"C:\Dev\Pricing", LinkKind.Folder)]
        [InlineData(@"C:\Dev\My.Repo\", LinkKind.Folder)]
        [InlineData(@"C:\Dev\Pricing\README.md", LinkKind.File)]
        [InlineData("https://github.com/sportingsolutions", LinkKind.Url)]
        [InlineData("http://localhost:5000/swagger", LinkKind.Url)]
        [InlineData("\"C:\\Dev\\Quoted\\Quoted.sln\"", LinkKind.Solution)]
        public void Detect_ReturnsKindFromShapeOfTarget(string target, LinkKind expected)
        {
            Assert.Equal(expected, LinkKindDetector.Detect(target));
        }

        [Fact]
        public void Detect_RejectsBlankTarget()
        {
            Assert.Throws<ArgumentException>(() => LinkKindDetector.Detect("  "));
        }

        [Theory]
        [InlineData(@"C:\Dev\Pricing\Pricing.sln", "Pricing")]
        [InlineData(@"C:\Dev\Pricing\", "Pricing")]
        [InlineData("https://github.com/sportingsolutions/", "github.com/sportingsolutions")]
        [InlineData("", "")]
        public void SuggestTitle_UsesFileOrFolderName(string target, string expected)
        {
            Assert.Equal(expected, LinkKindDetector.SuggestTitle(target));
        }

        [Fact]
        public void SuggestTitle_KeepsDotsInFolderNamesWhenKindKnown()
        {
            Assert.Equal("My.Repo", LinkKindDetector.SuggestTitle(@"C:\Dev\My.Repo", LinkKind.Folder));
            Assert.Equal("My", LinkKindDetector.SuggestTitle(@"C:\Dev\My.Repo"));
        }
    }
}

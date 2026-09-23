using System;
using UltimateStartPage.Core.ViewModels;
using Xunit;

namespace UltimateStartPage.Core.Tests
{
    public class SearchMatcherTests
    {
        [Theory]
        [InlineData(null, true)]
        [InlineData("", true)]
        [InlineData("   ", true)]
        [InlineData("harness", true)]
        [InlineData("HARNESS", true)]
        [InlineData("harness pricing", true)]
        [InlineData("harness tennis", false)]
        [InlineData("xyz", false)]
        public void Matches_RequiresEveryTermInSomeField(string? query, bool expected)
        {
            Assert.Equal(expected, SearchMatcher.Matches(query, "SS.Modelling.Harness", @"C:\Dev\Pricing\Harness.sln", null));
        }
    }
}

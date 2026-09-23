using System;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using UltimateStartPage.Core.Models;
using UltimateStartPage.Core.Services;
using Xunit;

namespace UltimateStartPage.Core.Tests
{
    public class VsRecentItemsReaderTests
    {
        private const string SampleJson = @"[
  {""Key"":""C:\\Dev\\Harness\\SS.Modelling.Harness.sln"",""Value"":{""LocalProperties"":{""FullPath"":""C:\\Dev\\Harness\\SS.Modelling.Harness.sln"",""Type"":0,""SourceControl"":null},""Remote"":null,""IsFavorite"":false,""LastAccessed"":""2026-09-20T10:00:00+00:00"",""IsLocal"":true}},
  {""Key"":""C:\\Dev\\My.Repo"",""Value"":{""LocalProperties"":{""FullPath"":""C:\\Dev\\My.Repo"",""Type"":1},""IsFavorite"":false,""LastAccessed"":""2026-09-22T09:00:00+00:00""}},
  {""Key"":""C:\\Dev\\Old\\Old.sln"",""Value"":{""LocalProperties"":{""FullPath"":""C:\\Dev\\Old\\Old.sln"",""Type"":0},""IsFavorite"":true,""LastAccessed"":""2025-01-01T09:00:00+00:00""}},
  {""Key"":""c:\\dev\\harness\\ss.modelling.harness.sln"",""Value"":{""LocalProperties"":{""FullPath"":""c:\\dev\\harness\\ss.modelling.harness.sln"",""Type"":0},""LastAccessed"":""2026-09-01T09:00:00+00:00""}},
  {""Key"":"""",""Value"":{}},
  42
]";

        [Fact]
        public void Parse_ReadsCodeContainersNewestFirstWithFavouritesOnTop()
        {
            var items = VsRecentItemsReader.Parse(WrapInSettingsXml(SampleJson));

            Assert.Equal(
                new[] { @"C:\Dev\Old\Old.sln", @"C:\Dev\My.Repo", @"C:\Dev\Harness\SS.Modelling.Harness.sln" },
                items.Select(i => i.FullPath).ToArray());
            Assert.True(items[0].IsFavorite);
        }

        [Fact]
        public void Parse_UsesContainerTypeSoDottedFolderNamesAreFolders()
        {
            var items = VsRecentItemsReader.Parse(WrapInSettingsXml(SampleJson));

            Assert.Equal(LinkKind.Folder, items.Single(i => i.FullPath.EndsWith("My.Repo")).Kind);
            Assert.Equal(LinkKind.Solution, items.Single(i => i.FullPath.EndsWith("Harness.sln")).Kind);
        }

        [Fact]
        public void Parse_KeepsMostRecentEntryWhenPathsDifferOnlyByCase()
        {
            var items = VsRecentItemsReader.Parse(WrapInSettingsXml(SampleJson));

            var harness = items.Single(i => i.FullPath.EndsWith("Harness.sln", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero), harness.LastAccessed);
        }

        [Fact]
        public void Parse_WhenCollectionAbsent_ReturnsEmpty()
        {
            var xml = "<content><indexed><collection name=\"Something.Else\"><value name=\"value\">[]</value></collection></indexed></content>";

            Assert.Empty(VsRecentItemsReader.Parse(xml));
        }

        [Fact]
        public async Task GetRecentItemsAsync_HonoursMaxItems()
        {
            var path = WriteTempFile(WrapInSettingsXml(SampleJson));
            try
            {
                var reader = CreateReader(path);

                var items = await reader.GetRecentItemsAsync(2, CancellationToken.None);

                Assert.Equal(2, items.Count);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public async Task GetRecentItemsAsync_WhenJsonMalformed_ReturnsEmptyInsteadOfThrowing()
        {
            var path = WriteTempFile(WrapInSettingsXml("[{ broken"));
            try
            {
                var items = await CreateReader(path).GetRecentItemsAsync(10, CancellationToken.None);

                Assert.Empty(items);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public async Task GetRecentItemsAsync_WhenXmlMalformed_ReturnsEmptyInsteadOfThrowing()
        {
            var path = WriteTempFile("<content><indexed>");
            try
            {
                Assert.Empty(await CreateReader(path).GetRecentItemsAsync(10, CancellationToken.None));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public async Task GetRecentItemsAsync_WhenFileMissing_ReturnsEmpty()
        {
            var reader = CreateReader(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xml"));

            Assert.Empty(await reader.GetRecentItemsAsync(10, CancellationToken.None));
        }

        private static VsRecentItemsReader CreateReader(string path) =>
            new VsRecentItemsReader(new VsRecentItemsOptions(path), NullLogger<VsRecentItemsReader>.Instance);

        private static string WriteTempFile(string content)
        {
            var path = Path.Combine(Path.GetTempPath(), "usp-" + Guid.NewGuid().ToString("N") + ".xml");
            File.WriteAllText(path, content);
            return path;
        }

        private static string WrapInSettingsXml(string json) =>
            "<?xml version=\"1.0\" encoding=\"utf-8\"?><content><indexed>" +
            "<collection name=\"CodeContainers.Offline\"><value name=\"value\">" + SecurityElement.Escape(json) + "</value></collection>" +
            "</indexed></content>";
    }
}

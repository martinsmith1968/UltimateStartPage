using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using UltimateStartPage.Core.Models;

namespace UltimateStartPage.Core.Services
{
    /// <summary>
    /// Persists link groups as JSON at %APPDATA%\UltimateStartPage\links.json (Decision #4).
    /// Pass a custom filePath to the constructor for test isolation.
    /// </summary>
    public class LinkRepository : ILinkRepository
    {
        private static readonly JsonSerializerOptions s_jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        private readonly string _filePath;

        /// <summary>Production constructor — uses %APPDATA%\UltimateStartPage\links.json.</summary>
        public LinkRepository()
            : this(DefaultFilePath()) { }

        /// <summary>Overload for testing with a custom file path.</summary>
        public LinkRepository(string filePath)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }

        private static string DefaultFilePath()
            => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "UltimateStartPage",
                "links.json");

        public async Task<IReadOnlyList<LinkGroup>> GetGroupsAsync()
        {
            if (!File.Exists(_filePath))
                return Array.Empty<LinkGroup>();

            try
            {
                var json = await ReadAllTextAsync(_filePath);
                var groups = JsonSerializer.Deserialize<List<LinkGroup>>(json, s_jsonOptions);
                return groups ?? (IReadOnlyList<LinkGroup>)Array.Empty<LinkGroup>();
            }
            catch (JsonException)
            {
                // Corrupt JSON — return empty rather than crashing. Log when logging is wired.
                return Array.Empty<LinkGroup>();
            }
            catch (IOException)
            {
                // File I/O error (in use, permissions, disk full) — return empty. Log when logging is wired.
                return Array.Empty<LinkGroup>();
            }
            catch (Exception)
            {
                // Unexpected error — return empty. Log when logging is wired.
                return Array.Empty<LinkGroup>();
            }
        }

        public async Task SaveGroupsAsync(IReadOnlyList<LinkGroup> groups)
        {
            try
            {
                EnsureDirectoryExists();
                var json = JsonSerializer.Serialize(groups, s_jsonOptions);
                await WriteAllTextAsync(_filePath, json);
            }
            catch (IOException)
            {
                // File I/O error during save (disk full, permissions) — throw to caller. Log when logging is wired.
                throw;
            }
            catch (Exception)
            {
                // Unexpected error during serialization or write — throw to caller. Log when logging is wired.
                throw;
            }
        }

        public async Task AddLinkAsync(string groupName, SolutionLink link)
        {
            if (string.IsNullOrWhiteSpace(groupName))
                throw new ArgumentNullException(nameof(groupName));
            if (link == null)
                throw new ArgumentNullException(nameof(link));

            var groups = (await GetGroupsAsync()).ToList();
            var group = groups.Find(g => g.Name == groupName);
            if (group == null)
            {
                group = new LinkGroup(groupName);
                groups.Add(group);
            }

            group.Links.Add(link);
            await SaveGroupsAsync(groups);
        }

        public async Task RemoveLinkAsync(string groupName, string filePath)
        {
            var groups = (await GetGroupsAsync()).ToList();
            var group = groups.Find(g => g.Name == groupName);
            if (group != null)
            {
                var link = group.Links.FirstOrDefault(l => l.FilePath == filePath);
                if (link != null)
                    group.Links.Remove(link);
            }

            await SaveGroupsAsync(groups);
        }

        private void EnsureDirectoryExists()
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        // net472 does not have File.ReadAllTextAsync — polyfill with Task.Run.
        private static Task<string> ReadAllTextAsync(string path)
            => Task.Run(() => File.ReadAllText(path));

        private static Task WriteAllTextAsync(string path, string contents)
            => Task.Run(() => File.WriteAllText(path, contents));
    }
}


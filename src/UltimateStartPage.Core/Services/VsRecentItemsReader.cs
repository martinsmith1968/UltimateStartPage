using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using UltimateStartPage.Core.Models;

namespace UltimateStartPage.Core.Services
{
    public sealed class VsRecentItemsOptions
    {
        public VsRecentItemsOptions(string applicationPrivateSettingsPath)
        {
            ApplicationPrivateSettingsPath = applicationPrivateSettingsPath
                ?? throw new ArgumentNullException(nameof(applicationPrivateSettingsPath));
        }

        /// <summary>
        /// Path to the instance's ApplicationPrivateSettings.xml, i.e.
        /// %LOCALAPPDATA%\Microsoft\VisualStudio\&lt;version&gt;_&lt;instance&gt;\ApplicationPrivateSettings.xml.
        /// </summary>
        public string ApplicationPrivateSettingsPath { get; }
    }

    /// <summary>
    /// Reads the "Open recent" list Visual Studio itself shows on its start window. Since VS 2019 it lives as a
    /// JSON array inside the <c>CodeContainers.Offline</c> collection of ApplicationPrivateSettings.xml.
    /// The format is undocumented, so parsing is deliberately forgiving: anything unexpected yields no items
    /// rather than an error on the start page.
    /// </summary>
    public sealed class VsRecentItemsReader : IRecentItemsSource
    {
        internal const string CollectionName = "CodeContainers.Offline";

        // CodeContainer "Type" values observed in the settings file.
        private const int FolderContainerType = 1;

        private readonly VsRecentItemsOptions _options;
        private readonly ILogger<VsRecentItemsReader> _logger;

        public VsRecentItemsReader(VsRecentItemsOptions options, ILogger<VsRecentItemsReader> logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>Runs on the thread pool: callers are on the UI thread, and the file is opened and parsed synchronously.</summary>
        public Task<IReadOnlyList<RecentItem>> GetRecentItemsAsync(int maxItems, CancellationToken cancellationToken) =>
            Task.Run(() => ReadRecentItemsAsync(maxItems, cancellationToken), cancellationToken);

        private async Task<IReadOnlyList<RecentItem>> ReadRecentItemsAsync(int maxItems, CancellationToken cancellationToken)
        {
            var path = _options.ApplicationPrivateSettingsPath;

            if (maxItems <= 0 || !File.Exists(path))
            {
                return Array.Empty<RecentItem>();
            }

            try
            {
                string xml;

                // VS keeps this file open, so share read/write access.
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, useAsync: true))
                using (var reader = new StreamReader(stream))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    xml = await reader.ReadToEndAsync().ConfigureAwait(false);
                }

                return Parse(xml).Take(maxItems).ToList();
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Could not read Visual Studio's recent items from {SettingsPath}", path);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Could not read Visual Studio's recent items from {SettingsPath}", path);
            }
            catch (XmlException ex)
            {
                _logger.LogWarning(ex, "Visual Studio's settings file {SettingsPath} is not valid XML", path);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Visual Studio's recent items in {SettingsPath} were not in the expected format", path);
            }

            return Array.Empty<RecentItem>();
        }

        /// <summary>Extracts recent items, newest first, de-duplicated by path.</summary>
        internal static IReadOnlyList<RecentItem> Parse(string applicationPrivateSettingsXml)
        {
            var document = XDocument.Parse(applicationPrivateSettingsXml);

            var json = document
                .Descendants("collection")
                .Where(c => string.Equals((string?)c.Attribute("name"), CollectionName, StringComparison.Ordinal))
                .Elements("value")
                .Where(v => string.Equals((string?)v.Attribute("name"), "value", StringComparison.Ordinal))
                .Select(v => v.Value)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(json))
            {
                return Array.Empty<RecentItem>();
            }

            var items = new List<RecentItem>();

            using (var parsed = JsonDocument.Parse(json!))
            {
                if (parsed.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return Array.Empty<RecentItem>();
                }

                foreach (var entry in parsed.RootElement.EnumerateArray())
                {
                    var item = ToRecentItem(entry);
                    if (item != null)
                    {
                        items.Add(item);
                    }
                }
            }

            return items
                .GroupBy(i => i.FullPath, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(i => i.LastAccessed ?? DateTimeOffset.MinValue).First())
                .OrderByDescending(i => i.IsFavorite)
                .ThenByDescending(i => i.LastAccessed ?? DateTimeOffset.MinValue)
                .ToList();
        }

        private static RecentItem? ToRecentItem(JsonElement entry)
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var value = GetObject(entry, "Value");
            var local = value.HasValue ? GetObject(value.Value, "LocalProperties") : null;

            var fullPath = (local.HasValue ? GetString(local.Value, "FullPath") : null) ?? GetString(entry, "Key");
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                return null;
            }

            var containerType = local.HasValue ? GetInt(local.Value, "Type") : null;
            var kind = containerType == FolderContainerType ? LinkKind.Folder : LinkKindDetector.Detect(fullPath!);

            DateTimeOffset? lastAccessed = null;
            var lastAccessedText = value.HasValue ? GetString(value.Value, "LastAccessed") : null;
            if (DateTimeOffset.TryParse(lastAccessedText, out var parsedDate))
            {
                lastAccessed = parsedDate;
            }

            var isFavorite = value.HasValue
                && value.Value.TryGetProperty("IsFavorite", out var favorite)
                && favorite.ValueKind == JsonValueKind.True;

            return new RecentItem(fullPath!, kind, lastAccessed, isFavorite);
        }

        private static JsonElement? GetObject(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out var child) && child.ValueKind == JsonValueKind.Object
                ? child
                : (JsonElement?)null;
        }

        private static string? GetString(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out var child) && child.ValueKind == JsonValueKind.String
                ? child.GetString()
                : null;
        }

        private static int? GetInt(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out var child)
                && child.ValueKind == JsonValueKind.Number
                && child.TryGetInt32(out var number)
                    ? number
                    : (int?)null;
        }
    }
}

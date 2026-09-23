using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UltimateStartPage.Core.Models;

namespace UltimateStartPage.Core.Services
{
    /// <summary>
    /// Stores the layout as indented JSON. Writes go to a temp file first and are then swapped in, so a crash
    /// mid-save never leaves a half-written layout. An unreadable file is moved aside rather than overwritten.
    /// <para>
    /// Every Visual Studio instance shares the file. The store remembers a hash of the file as it last read or wrote
    /// it: <see cref="SaveAsync"/> refuses to overwrite a file that has changed since (the caller merges and retries,
    /// see <see cref="LayoutMerger"/>), and a file watcher raises <see cref="ExternalChange"/> so the other instances
    /// can pick the change up. The check and the write are not atomic across
    /// processes, but two instances saving within the same few milliseconds is very unlikely.
    /// </para>
    /// </summary>
    public sealed class JsonLayoutStore : ILayoutStore, IDisposable
    {
        internal static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

        private readonly LayoutStoreOptions _options;
        private readonly ILogger<JsonLayoutStore> _logger;
        private readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);
        private readonly object _watcherGate = new object();
        private readonly Timer _changeTimer;
        private FileSystemWatcher? _watcher;
        private bool _disposed;

        // SHA-256 of the layout file as this store last read or wrote it; null if there was no file. Guarded by _fileLock.
        private byte[]? _knownContentHash;

        public JsonLayoutStore(LayoutStoreOptions options, ILogger<JsonLayoutStore> logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            FilePath = options.FilePath;
            _changeTimer = new Timer(_ => _ = CheckForExternalChangeAsync(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public event EventHandler? ExternalChange;

        public string FilePath { get; }

        // Callers are usually on the UI thread. Opening a FileStream is synchronous, awaits that complete immediately
        // (a free lock, a small file) don't leave the caller's thread, and the layout may live on a network share. So
        // every entry point moves to the thread pool before touching the disk.

        public Task<StartPageLayout> LoadAsync(CancellationToken cancellationToken) =>
            Task.Run(() => LoadCoreAsync(cancellationToken), cancellationToken);

        public Task SaveAsync(StartPageLayout layout, CancellationToken cancellationToken) =>
            Task.Run(() => SaveCoreAsync(layout, cancellationToken), cancellationToken);

        public Task<bool> HasExternalChangesAsync(CancellationToken cancellationToken) =>
            Task.Run(() => HasExternalChangesUnderLockAsync(cancellationToken), cancellationToken);

        public Task<StartPageLayout> ImportAsync(string path, CancellationToken cancellationToken) =>
            Task.Run(() => ImportCoreAsync(path, cancellationToken), cancellationToken);

        public Task ExportAsync(StartPageLayout layout, string path, CancellationToken cancellationToken) =>
            Task.Run(() => ExportCoreAsync(layout, path, cancellationToken), cancellationToken);

        private async Task<StartPageLayout> LoadCoreAsync(CancellationToken cancellationToken)
        {
            StartWatching();

            await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var bytes = await ReadBytesIfExistsAsync(FilePath, cancellationToken).ConfigureAwait(false);
                _knownContentHash = Hash(bytes);

                if (bytes == null)
                {
                    _logger.LogInformation("No layout found at {LayoutPath}; starting with the default layout", FilePath);
                    return DefaultLayout.Create();
                }

                try
                {
                    var layout = await ParseAsync(bytes, FilePath, cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation(
                        "Loaded {SectionCount} sections from {LayoutPath}", layout.Sections.Count, FilePath);
                    return layout;
                }
                catch (LayoutFormatException ex)
                {
                    var backupPath = MoveAsideCorruptFile();
                    if (backupPath != FilePath)
                    {
                        _knownContentHash = null;
                    }

                    _logger.LogWarning(
                        ex,
                        "Layout at {LayoutPath} could not be read and was moved to {BackupPath}; starting with the default layout",
                        FilePath,
                        backupPath);
                    return DefaultLayout.Create();
                }
            }
            finally
            {
                _fileLock.Release();
            }
        }

        private async Task SaveCoreAsync(StartPageLayout layout, CancellationToken cancellationToken)
        {
            var bytes = Serialize(layout);

            await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (await HasExternalChangesCoreAsync(cancellationToken).ConfigureAwait(false))
                {
                    throw new LayoutConflictException(
                        $"'{FilePath}' was changed by another Visual Studio instance or program since it was loaded.");
                }

                await WriteBytesAtomicallyAsync(FilePath, bytes, cancellationToken).ConfigureAwait(false);
                _knownContentHash = Hash(bytes);
            }
            finally
            {
                _fileLock.Release();
            }

            // The first save may have just created the folder, so watching may only now be possible.
            StartWatching();
            _logger.LogDebug("Saved layout to {LayoutPath}", FilePath);
        }

        private async Task<bool> HasExternalChangesUnderLockAsync(CancellationToken cancellationToken)
        {
            await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await HasExternalChangesCoreAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _fileLock.Release();
            }
        }

        private async Task<StartPageLayout> ImportCoreAsync(string path, CancellationToken cancellationToken)
        {
            var bytes = await ReadBytesIfExistsAsync(path, cancellationToken).ConfigureAwait(false)
                ?? throw new FileNotFoundException("The layout file to import does not exist.", path);

            var layout = await ParseAsync(bytes, path, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Imported {SectionCount} sections from {ImportPath}", layout.Sections.Count, path);
            return layout;
        }

        private async Task ExportCoreAsync(StartPageLayout layout, string path, CancellationToken cancellationToken)
        {
            var bytes = Serialize(layout);

            await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await WriteBytesAtomicallyAsync(path, bytes, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _fileLock.Release();
            }

            _logger.LogInformation("Exported layout to {ExportPath}", path);
        }

        public void Dispose()
        {
            lock (_watcherGate)
            {
                _disposed = true;
                _watcher?.Dispose();
                _watcher = null;
            }

            _changeTimer.Dispose();
        }

        /// <summary>
        /// A deleted file doesn't count: there's nothing there to lose, and the next save simply recreates it.
        /// Sync clients also sometimes delete and recreate a file while updating it. Caller holds _fileLock.
        /// </summary>
        private async Task<bool> HasExternalChangesCoreAsync(CancellationToken cancellationToken)
        {
            var current = await ReadBytesIfExistsAsync(FilePath, cancellationToken).ConfigureAwait(false);
            return current != null && !HashEquals(Hash(current), _knownContentHash);
        }

        private void StartWatching()
        {
            if (!_options.WatchForExternalChanges)
            {
                return;
            }

            lock (_watcherGate)
            {
                if (_watcher != null || _disposed)
                {
                    return;
                }

                var directory = Path.GetDirectoryName(Path.GetFullPath(FilePath));
                if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                {
                    return;
                }

                try
                {
                    var watcher = new FileSystemWatcher(directory, Path.GetFileName(FilePath))
                    {
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    };

                    // Our own saves fire these too; CheckForExternalChangeAsync tells them apart by content.
                    watcher.Changed += OnFileEvent;
                    watcher.Created += OnFileEvent;
                    watcher.Renamed += OnFileEvent;
                    watcher.Error += OnWatcherError;
                    watcher.EnableRaisingEvents = true;

                    _watcher = watcher;
                    _logger.LogDebug("Watching {LayoutPath} for changes made elsewhere", FilePath);
                }
                catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is PlatformNotSupportedException)
                {
                    // Some network shares don't support change notifications. Saves are still conflict-checked.
                    _logger.LogWarning(ex, "Cannot watch {LayoutPath} for changes; edits made elsewhere need a manual reload", FilePath);
                }
            }
        }

        private void OnFileEvent(object sender, FileSystemEventArgs e) => ScheduleExternalChangeCheck();

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            // Usually a buffer overflow: events were dropped, so check anyway.
            _logger.LogDebug(e.GetException(), "The layout file watcher reported an error");
            ScheduleExternalChangeCheck();
        }

        /// <summary>Restarts the quiet-period timer, so a burst of events leads to one check.</summary>
        private void ScheduleExternalChangeCheck()
        {
            try
            {
                _changeTimer.Change(_options.ExternalChangeDelay, Timeout.InfiniteTimeSpan);
            }
            catch (ObjectDisposedException)
            {
                // Disposed while an event was in flight.
            }
        }

        private async Task CheckForExternalChangeAsync()
        {
            try
            {
                if (!await HasExternalChangesAsync(CancellationToken.None).ConfigureAwait(false))
                {
                    return;
                }

                _logger.LogInformation("{LayoutPath} was changed outside this Visual Studio instance", FilePath);
                ExternalChange?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // Whoever changed the file may still have it open; look again shortly.
                _logger.LogDebug(ex, "Could not read {LayoutPath} to check for changes; retrying", FilePath);
                ScheduleExternalChangeCheck();
            }
            catch (ObjectDisposedException)
            {
                // Shutting down.
            }
            catch (Exception ex)
            {
                // This runs on a timer thread, so nothing can be allowed to escape.
                _logger.LogWarning(ex, "Checking {LayoutPath} for outside changes failed", FilePath);
            }
        }

        private static async Task<byte[]?> ReadBytesIfExistsAsync(string path, CancellationToken cancellationToken)
        {
            try
            {
                using (var stream = new FileStream(
                    path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, useAsync: true))
                using (var buffer = new MemoryStream())
                {
                    await stream.CopyToAsync(buffer, 81920, cancellationToken).ConfigureAwait(false);
                    return buffer.ToArray();
                }
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
        }

        private static async Task<StartPageLayout> ParseAsync(byte[] bytes, string path, CancellationToken cancellationToken)
        {
            StartPageLayout? layout;

            try
            {
                // Deserialising from a stream (rather than the bytes directly) skips a UTF-8 BOM, which editors add.
                using (var stream = new MemoryStream(bytes, writable: false))
                {
                    layout = await JsonSerializer
                        .DeserializeAsync<StartPageLayout>(stream, SerializerOptions, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            catch (JsonException ex)
            {
                throw new LayoutFormatException($"'{path}' is not a valid start page layout: {ex.Message}", ex);
            }

            if (layout == null)
            {
                throw new LayoutFormatException($"'{path}' is empty.");
            }

            if (layout.SchemaVersion > StartPageLayout.CurrentSchemaVersion)
            {
                throw new LayoutFormatException(
                    $"'{path}' was written by a newer version of Ultimate Start Page (schema {layout.SchemaVersion}).");
            }

            return Normalise(layout);
        }

        private static byte[] Serialize(StartPageLayout layout)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            return JsonSerializer.SerializeToUtf8Bytes(Normalise(layout), SerializerOptions);
        }

        /// <summary>Caller holds _fileLock, which also keeps two writes from sharing the temp file.</summary>
        private static async Task WriteBytesAtomicallyAsync(string path, byte[] bytes, CancellationToken cancellationToken)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = path + ".tmp";

            try
            {
                using (var stream = new FileStream(
                    tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
                {
                    await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
                }

                if (File.Exists(path))
                {
                    File.Replace(tempPath, path, destinationBackupFileName: null);
                }
                else
                {
                    File.Move(tempPath, path);
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        private static byte[]? Hash(byte[]? bytes)
        {
            if (bytes == null)
            {
                return null;
            }

            using (var sha = SHA256.Create())
            {
                return sha.ComputeHash(bytes);
            }
        }

        private static bool HashEquals(byte[]? a, byte[]? b) =>
            a == null || b == null ? a == b : a.SequenceEqual(b);

        private string MoveAsideCorruptFile()
        {
            var backupPath = $"{FilePath}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";

            try
            {
                File.Move(FilePath, backupPath);
                return backupPath;
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Could not move the unreadable layout at {LayoutPath} aside", FilePath);
                return FilePath;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Could not move the unreadable layout at {LayoutPath} aside", FilePath);
                return FilePath;
            }
        }

        /// <summary>
        /// Repairs hand-edited or imported layouts: drops null entries and links without a target, fills in
        /// missing ids and titles, and de-duplicates ids so the UI can rely on them being unique.
        /// </summary>
        internal static StartPageLayout Normalise(StartPageLayout layout)
        {
            var seenIds = new HashSet<Guid>();
            var sections = new List<Section>();

            foreach (var section in (layout.Sections ?? new List<Section>()).Where(s => s != null))
            {
                var links = new List<LinkItem>();

                foreach (var link in (section.Links ?? new List<LinkItem>())
                    .Where(l => l != null && !string.IsNullOrWhiteSpace(l.Target)))
                {
                    var target = link.Target.Trim();
                    links.Add(new LinkItem
                    {
                        Id = UniqueId(link.Id, seenIds),
                        Title = string.IsNullOrWhiteSpace(link.Title) ? LinkKindDetector.SuggestTitle(target, link.Kind) : link.Title.Trim(),
                        Target = target,
                        Kind = link.Kind,
                        Description = string.IsNullOrWhiteSpace(link.Description) ? null : link.Description!.Trim(),
                    });
                }

                sections.Add(new Section
                {
                    Id = UniqueId(section.Id, seenIds),
                    Title = string.IsNullOrWhiteSpace(section.Title) ? "Untitled section" : section.Title.Trim(),
                    IsCollapsed = section.IsCollapsed,
                    Links = links,
                });
            }

            return new StartPageLayout { SchemaVersion = StartPageLayout.CurrentSchemaVersion, Sections = sections };
        }

        private static Guid UniqueId(Guid id, HashSet<Guid> seenIds)
        {
            var candidate = id == Guid.Empty || seenIds.Contains(id) ? Guid.NewGuid() : id;
            seenIds.Add(candidate);
            return candidate;
        }

        private static JsonSerializerOptions CreateSerializerOptions()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }
    }
}

using System;
using System.IO;

namespace UltimateStartPage.Core.Services
{
    public sealed class LayoutStoreOptions
    {
        public LayoutStoreOptions(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("A layout file path is required.", nameof(filePath));
            }

            FilePath = filePath;
        }

        public string FilePath { get; }

        /// <summary>Watch the file so edits made by other Visual Studio instances (or by hand) are picked up.</summary>
        public bool WatchForExternalChanges { get; set; } = true;

        /// <summary>
        /// How long the file must be quiet before an outside change is reported. Writers and sync clients often touch
        /// a file several times in a row, and this waits for them to finish.
        /// </summary>
        public TimeSpan ExternalChangeDelay { get; set; } = TimeSpan.FromMilliseconds(300);

        /// <summary>%APPDATA%\UltimateStartPage\layout.json — roams with the Windows profile and is shared by every VS instance.</summary>
        public static string DefaultFilePath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "UltimateStartPage",
                "layout.json");
    }
}

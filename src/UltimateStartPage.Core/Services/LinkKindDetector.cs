using System;
using System.Collections.Generic;
using UltimateStartPage.Core.Models;

namespace UltimateStartPage.Core.Services
{
    /// <summary>Works out what kind of link a target string is, from its shape alone (no disk access).</summary>
    public static class LinkKindDetector
    {
        private static readonly HashSet<string> SolutionExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".sln", ".slnx", ".slnf" };

        private static readonly HashSet<string> ProjectExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".csproj", ".vbproj", ".fsproj", ".vcxproj", ".sqlproj", ".esproj", ".pyproj",
                ".njsproj", ".wapproj", ".shproj", ".dcproj", ".proj",
            };

        public static LinkKind Detect(string target)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                throw new ArgumentException("A link target is required.", nameof(target));
            }

            var trimmed = target.Trim().Trim('"');

            if (IsWebUrl(trimmed))
            {
                return LinkKind.Url;
            }

            if (trimmed.EndsWith("\\", StringComparison.Ordinal) || trimmed.EndsWith("/", StringComparison.Ordinal))
            {
                return LinkKind.Folder;
            }

            var extension = GetExtension(trimmed);

            if (extension.Length == 0)
            {
                return LinkKind.Folder;
            }

            if (SolutionExtensions.Contains(extension))
            {
                return LinkKind.Solution;
            }

            if (ProjectExtensions.Contains(extension))
            {
                return LinkKind.Project;
            }

            return LinkKind.File;
        }

        public static bool IsWebUrl(string target)
        {
            return Uri.TryCreate(target, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        /// <summary>A friendly default title: the file name without extension, or the last folder name.</summary>
        /// <param name="target">Path or URL.</param>
        /// <param name="kind">When known, stops a folder such as "My.Repo" being treated as a file with an extension.</param>
        public static string SuggestTitle(string target, LinkKind? kind = null)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                return string.Empty;
            }

            var trimmed = target.Trim().Trim('"');

            if (IsWebUrl(trimmed))
            {
                var uri = new Uri(trimmed);
                return uri.Host + uri.AbsolutePath.TrimEnd('/');
            }

            var withoutSlash = trimmed.TrimEnd('\\', '/');
            var name = GetFileName(withoutSlash);
            if (kind == LinkKind.Folder)
            {
                return name;
            }

            var extension = GetExtension(name);

            return extension.Length > 0 ? name.Substring(0, name.Length - extension.Length) : name;
        }

        // Path.GetExtension/GetFileName only understand the current OS's separators. Stored targets are
        // Windows paths, so handle both separators explicitly to keep behaviour identical everywhere.
        private static string GetFileName(string path)
        {
            var index = path.LastIndexOfAny(new[] { '\\', '/' });
            return index >= 0 ? path.Substring(index + 1) : path;
        }

        private static string GetExtension(string path)
        {
            var name = GetFileName(path);
            var dot = name.LastIndexOf('.');
            return dot > 0 && dot < name.Length - 1 ? name.Substring(dot) : string.Empty;
        }
    }
}

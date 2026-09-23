using System;
using System.Linq;

namespace UltimateStartPage.Core.ViewModels
{
    /// <summary>Case-insensitive filter: every space-separated term must appear in at least one field.</summary>
    public static class SearchMatcher
    {
        public static bool Matches(string? query, params string?[] fields)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            var terms = query!.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            return terms.All(term => fields.Any(field =>
                field != null && field.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0));
        }
    }
}

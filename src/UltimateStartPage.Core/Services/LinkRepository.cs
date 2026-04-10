using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UltimateStartPage.Core.Models;

namespace UltimateStartPage.Core.Services
{
    // Stub implementation — in-memory only.
    // Real persistence via WritableSettingsStore will be added in the VSIX project once
    // the VS SDK is available; this class lives in Core so it can be unit-tested freely.
    public class LinkRepository : ILinkRepository
    {
        private readonly List<LinkGroup> _groups = new List<LinkGroup>();

        public Task<IReadOnlyList<LinkGroup>> GetGroupsAsync()
        {
            return Task.FromResult<IReadOnlyList<LinkGroup>>(_groups.AsReadOnly());
        }

        public Task SaveGroupsAsync(IReadOnlyList<LinkGroup> groups)
        {
            _groups.Clear();
            foreach (var group in groups)
                _groups.Add(group);

            return Task.CompletedTask;
        }

        public Task AddLinkAsync(string groupName, SolutionLink link)
        {
            if (string.IsNullOrWhiteSpace(groupName))
                throw new ArgumentNullException(nameof(groupName));
            if (link == null)
                throw new ArgumentNullException(nameof(link));

            var group = _groups.Find(g => g.Name == groupName);
            if (group == null)
            {
                group = new LinkGroup(groupName);
                _groups.Add(group);
            }

            group.Links.Add(link);
            return Task.CompletedTask;
        }

        public Task RemoveLinkAsync(string groupName, string filePath)
        {
            var group = _groups.Find(g => g.Name == groupName);
            if (group != null)
            {
                var link = group.Links.FirstOrDefault(l => l.FilePath == filePath);
                if (link != null)
                    group.Links.Remove(link);
            }

            return Task.CompletedTask;
        }
    }
}

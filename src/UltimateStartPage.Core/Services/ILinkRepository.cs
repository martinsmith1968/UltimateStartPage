using System.Collections.Generic;
using System.Threading.Tasks;
using UltimateStartPage.Core.Models;

namespace UltimateStartPage.Core.Services
{
    public interface ILinkRepository
    {
        Task<IReadOnlyList<LinkGroup>> GetGroupsAsync();
        Task SaveGroupsAsync(IReadOnlyList<LinkGroup> groups);
        Task AddLinkAsync(string groupName, SolutionLink link);
        Task RemoveLinkAsync(string groupName, string filePath);
    }
}

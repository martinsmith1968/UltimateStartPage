using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UltimateStartPage.Core.Models;

namespace UltimateStartPage.Core.Services
{
    /// <summary>Supplies Visual Studio's own recent solutions/folders list, newest first.</summary>
    public interface IRecentItemsSource
    {
        Task<IReadOnlyList<RecentItem>> GetRecentItemsAsync(int maxItems, CancellationToken cancellationToken);
    }
}

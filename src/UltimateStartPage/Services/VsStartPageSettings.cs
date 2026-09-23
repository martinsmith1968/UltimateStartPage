using System;
using UltimateStartPage.Core.Services;
using UltimateStartPage.Options;

namespace UltimateStartPage.Services
{
    /// <summary>
    /// Adapts the Tools &gt; Options page to the core settings interface. Values are read on every access so
    /// changes apply the next time the page refreshes, without a restart.
    /// </summary>
    internal sealed class VsStartPageSettings : IStartPageSettings
    {
        public bool ShowRecentItems => GeneralOptions.Instance.ShowRecentItems;

        public int MaxRecentItems => Math.Max(1, Math.Min(100, GeneralOptions.Instance.MaxRecentItems));

        public bool ShowGetStartedActions => GeneralOptions.Instance.ShowGetStartedActions;
    }
}

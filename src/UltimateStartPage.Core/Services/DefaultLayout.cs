using System.Collections.Generic;
using UltimateStartPage.Core.Models;

namespace UltimateStartPage.Core.Services
{
    /// <summary>What a first-time user sees: one empty section ready to fill, plus a couple of handy web links.</summary>
    public static class DefaultLayout
    {
        public static StartPageLayout Create()
        {
            return new StartPageLayout
            {
                Sections = new List<Section>
                {
                    new Section { Title = "Favourites" },
                    new Section
                    {
                        Title = "Links",
                        Links = new List<LinkItem>
                        {
                            new LinkItem
                            {
                                Title = "Visual Studio documentation",
                                Target = "https://learn.microsoft.com/visualstudio/",
                                Kind = LinkKind.Url,
                            },
                            new LinkItem
                            {
                                Title = ".NET documentation",
                                Target = "https://learn.microsoft.com/dotnet/",
                                Kind = LinkKind.Url,
                            },
                        },
                    },
                },
            };
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UltimateStartPage.Core.Models;

namespace UltimateStartPage.Core.Services
{
    public sealed class LayoutMergeResult
    {
        public LayoutMergeResult(StartPageLayout layout, IReadOnlyList<string> conflicts)
        {
            Layout = layout;
            Conflicts = conflicts;
        }

        public StartPageLayout Layout { get; }

        /// <summary>Places where both sides changed the same thing, and which one was kept. For the log.</summary>
        public IReadOnlyList<string> Conflicts { get; }
    }

    /// <summary>
    /// Three-way merge of two layouts that grew from a common ancestor, e.g. two Visual Studio instances that both
    /// edited the shared layout file. Sections and links are matched by id, so changes to different things combine.
    /// <list type="bullet">
    /// <item>A field changed on one side only takes that side's value. Changed differently on both, <c>mine</c> wins
    /// (it is the most recent action) and a conflict is reported.</item>
    /// <item>A deletion wins unless the other side changed the thing, so a merge never silently throws away an edit.</item>
    /// <item>Order comes from the side that reordered (<c>mine</c> if both), with items that only the other side has
    /// inserted after whatever preceded them there.</item>
    /// </list>
    /// </summary>
    public static class LayoutMerger
    {
        public static LayoutMergeResult Merge(StartPageLayout ancestor, StartPageLayout mine, StartPageLayout theirs)
        {
            if (ancestor == null)
            {
                throw new ArgumentNullException(nameof(ancestor));
            }

            if (mine == null)
            {
                throw new ArgumentNullException(nameof(mine));
            }

            if (theirs == null)
            {
                throw new ArgumentNullException(nameof(theirs));
            }

            var b = new Side(ancestor);
            var m = new Side(mine);
            var t = new Side(theirs);
            var conflicts = new List<string>();

            // Links first: which survive, their fields, and which section each ends up in.
            var links = new Dictionary<Guid, PlacedLink>();
            foreach (var id in m.LinkOrder.Concat(t.LinkOrder).Distinct())
            {
                b.Links.TryGetValue(id, out var bl);
                m.Links.TryGetValue(id, out var ml);
                t.Links.TryGetValue(id, out var tl);

                if (ml != null && tl != null)
                {
                    links[id] = MergeLink(bl, ml, tl, conflicts);
                }
                else if (Survives(bl, ml ?? tl!, IsLinkChanged, conflicts, "link"))
                {
                    links[id] = ml ?? tl!;
                }
            }

            // A section survives a deletion on one side if the other side changed its title or state, or if a
            // surviving link now lives in it.
            var linkSections = new HashSet<Guid>(links.Values.Select(l => l.SectionId));
            var sections = new Dictionary<Guid, Section>();
            foreach (var id in m.SectionOrder.Concat(t.SectionOrder).Distinct())
            {
                b.Sections.TryGetValue(id, out var bs);
                m.Sections.TryGetValue(id, out var ms);
                t.Sections.TryGetValue(id, out var ts);

                if (ms != null && ts != null)
                {
                    sections[id] = MergeSection(bs, ms, ts, conflicts);
                }
                else if (linkSections.Contains(id) || Survives(bs, ms ?? ts!, IsSectionChanged, conflicts, "section"))
                {
                    sections[id] = Copy(ms ?? ts!);
                }
            }

            var result = new StartPageLayout();
            foreach (var sectionId in MergeOrder(b.SectionOrder, m.SectionOrder, t.SectionOrder, sections.Keys))
            {
                var section = sections[sectionId];
                var here = links.Values.Where(l => l.SectionId == sectionId).Select(l => l.Link.Id);
                section.Links = MergeOrder(b.LinksIn(sectionId), m.LinksIn(sectionId), t.LinksIn(sectionId), here)
                    .Select(linkId => Copy(links[linkId].Link))
                    .ToList();
                result.Sections.Add(section);
            }

            return new LayoutMergeResult(result, conflicts);
        }

        /// <summary>Same sections and links, in the same order, with the same values.</summary>
        public static bool AreEquivalent(StartPageLayout a, StartPageLayout b)
        {
            return a.Sections.Count == b.Sections.Count
                && a.Sections.Zip(b.Sections, (x, y) =>
                        x.Id == y.Id
                        && x.Title == y.Title
                        && x.IsCollapsed == y.IsCollapsed
                        && x.Links.Count == y.Links.Count
                        && x.Links.Zip(y.Links, (p, q) => p.Id == q.Id && !IsLinkContentChanged(p, q)).All(same => same))
                    .All(same => same);
        }

        private static PlacedLink MergeLink(PlacedLink? b, PlacedLink m, PlacedLink t, List<string> conflicts)
        {
            var name = m.Link.Title;
            var link = new LinkItem
            {
                Id = m.Link.Id,
                Title = Pick(b?.Link.Title ?? string.Empty, m.Link.Title, t.Link.Title, b != null, () => Conflict(conflicts, "title", "link", name)),
                Description = Pick(b?.Link.Description, m.Link.Description, t.Link.Description, b != null, () => Conflict(conflicts, "description", "link", name)),
            };

            // The kind is derived from the target, so they change together.
            (link.Target, link.Kind) = Pick(
                b == null ? default : (b.Link.Target, b.Link.Kind),
                (m.Link.Target, m.Link.Kind),
                (t.Link.Target, t.Link.Kind),
                b != null,
                () => Conflict(conflicts, "target", "link", name));

            var sectionId = Pick(b?.SectionId, m.SectionId, t.SectionId, b != null, () => Conflict(conflicts, "section", "link", name));
            return new PlacedLink(link, sectionId);
        }

        private static Section MergeSection(Section? b, Section m, Section t, List<string> conflicts)
        {
            return new Section
            {
                Id = m.Id,
                Title = Pick(b?.Title ?? string.Empty, m.Title, t.Title, b != null, () => Conflict(conflicts, "title", "section", m.Title)),
                IsCollapsed = Pick(b?.IsCollapsed, m.IsCollapsed, t.IsCollapsed, b != null, () => { }),
            };
        }

        /// <summary>Only one side still has the item: it was added there, or deleted on the other side.</summary>
        private static bool Survives<T>(T? ancestor, T kept, Func<T, T, bool> isChanged, List<string> conflicts, string what)
            where T : class
        {
            if (ancestor == null)
            {
                return true; // added
            }

            if (!isChanged(ancestor, kept))
            {
                return false; // deleted, and nobody minded
            }

            conflicts.Add($"A {what} was deleted in one window but changed in the other; it was kept.");
            return true;
        }

        private static T Pick<T>(T ancestor, T mine, T theirs, bool hasAncestor, Action onConflict)
        {
            var comparer = EqualityComparer<T>.Default;

            if (comparer.Equals(mine, theirs) || (hasAncestor && comparer.Equals(theirs, ancestor)))
            {
                return mine;
            }

            if (hasAncestor && comparer.Equals(mine, ancestor))
            {
                return theirs;
            }

            onConflict();
            return mine;
        }

        private static T Pick<T>(T? ancestor, T mine, T theirs, bool hasAncestor, Action onConflict)
            where T : struct =>
            Pick<T>(ancestor.GetValueOrDefault(), mine, theirs, hasAncestor, onConflict);

        private static void Conflict(List<string> conflicts, string field, string what, string name) =>
            conflicts.Add($"Both windows changed the {field} of {what} \"{name}\"; this window's change was kept.");

        /// <summary>
        /// The order of <paramref name="survivors"/>. The side that reordered what it shares with the ancestor sets
        /// the order (mine if both did); items only the other side has go after whatever preceded them there.
        /// </summary>
        private static List<Guid> MergeOrder(
            IReadOnlyList<Guid> ancestor, IReadOnlyList<Guid> mine, IReadOnlyList<Guid> theirs, IEnumerable<Guid> survivors)
        {
            var keep = new HashSet<Guid>(survivors);
            var mineReordered = IsReordered(ancestor, mine);
            var primary = mineReordered || !IsReordered(ancestor, theirs) ? mine : theirs;
            var secondary = ReferenceEquals(primary, mine) ? theirs : mine;

            var result = primary.Where(keep.Contains).ToList();
            var placed = new HashSet<Guid>(result);
            Guid? previous = null;

            foreach (var id in secondary.Where(keep.Contains))
            {
                if (placed.Add(id))
                {
                    result.Insert(previous.HasValue ? result.IndexOf(previous.Value) + 1 : 0, id);
                }

                previous = id;
            }

            // Defensive: anything neither side listed (shouldn't happen) goes last rather than vanishing.
            result.AddRange(keep.Where(id => !placed.Contains(id)));
            return result;
        }

        private static bool IsReordered(IReadOnlyList<Guid> ancestor, IReadOnlyList<Guid> side)
        {
            var common = new HashSet<Guid>(ancestor.Intersect(side));
            return !ancestor.Where(common.Contains).SequenceEqual(side.Where(common.Contains));
        }

        private static bool IsLinkChanged(PlacedLink ancestor, PlacedLink side) =>
            ancestor.SectionId != side.SectionId || IsLinkContentChanged(ancestor.Link, side.Link);

        private static bool IsLinkContentChanged(LinkItem a, LinkItem b) =>
            a.Title != b.Title || a.Target != b.Target || a.Kind != b.Kind || a.Description != b.Description;

        private static bool IsSectionChanged(Section ancestor, Section side) =>
            ancestor.Title != side.Title || ancestor.IsCollapsed != side.IsCollapsed;

        private static Section Copy(Section s) => new Section { Id = s.Id, Title = s.Title, IsCollapsed = s.IsCollapsed };

        private static LinkItem Copy(LinkItem l) =>
            new LinkItem { Id = l.Id, Title = l.Title, Target = l.Target, Kind = l.Kind, Description = l.Description };

        private sealed class PlacedLink
        {
            public PlacedLink(LinkItem link, Guid sectionId)
            {
                Link = link;
                SectionId = sectionId;
            }

            public LinkItem Link { get; }

            public Guid SectionId { get; }
        }

        /// <summary>One layout indexed by id. Duplicate ids (which the store never writes) keep the first.</summary>
        private sealed class Side
        {
            private readonly Dictionary<Guid, List<Guid>> _linksBySection = new Dictionary<Guid, List<Guid>>();

            public Side(StartPageLayout layout)
            {
                foreach (var section in layout.Sections)
                {
                    if (Sections.ContainsKey(section.Id))
                    {
                        continue;
                    }

                    Sections[section.Id] = section;
                    SectionOrder.Add(section.Id);
                    var ids = new List<Guid>();
                    _linksBySection[section.Id] = ids;

                    foreach (var link in section.Links.Where(l => !Links.ContainsKey(l.Id)))
                    {
                        Links[link.Id] = new PlacedLink(link, section.Id);
                        LinkOrder.Add(link.Id);
                        ids.Add(link.Id);
                    }
                }
            }

            public Dictionary<Guid, Section> Sections { get; } = new Dictionary<Guid, Section>();

            public List<Guid> SectionOrder { get; } = new List<Guid>();

            public Dictionary<Guid, PlacedLink> Links { get; } = new Dictionary<Guid, PlacedLink>();

            public List<Guid> LinkOrder { get; } = new List<Guid>();

            public IReadOnlyList<Guid> LinksIn(Guid sectionId) =>
                _linksBySection.TryGetValue(sectionId, out var ids) ? ids : (IReadOnlyList<Guid>)Array.Empty<Guid>();
        }
    }
}

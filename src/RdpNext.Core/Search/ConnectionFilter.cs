using RdpNext.Core.Model;

namespace RdpNext.Core.Search;

public sealed record FilterResult(IReadOnlySet<Guid> Visible, Server? FirstMatch);

public static class ConnectionFilter
{
    public static FilterResult Apply(Group root, string? query)
    {
        var visible = new HashSet<Guid> { root.Id };
        if (string.IsNullOrWhiteSpace(query))
        {
            AddSubtree(root, visible);
            return new FilterResult(visible, null);
        }

        Server? firstMatch = null;
        Visit(root, SearchText.Fold(query.Trim()), visible, ref firstMatch);
        return new FilterResult(visible, firstMatch);
    }

    // Grubun alt ağacında görünür bir şey varsa true döner.
    private static bool Visit(Group group, string query, HashSet<Guid> visible, ref Server? firstMatch)
    {
        var any = false;
        foreach (var child in group.Children)
        {
            switch (child)
            {
                case Group g when SearchText.Matches(g.Name, query):
                    visible.Add(g.Id);
                    firstMatch ??= g.AllServers().FirstOrDefault();
                    AddSubtree(g, visible);
                    any = true;
                    break;
                case Group g when Visit(g, query, visible, ref firstMatch):
                    visible.Add(g.Id);
                    any = true;
                    break;
                case Server s when SearchText.Matches(s.Name, query) || SearchText.Matches(s.Host, query):
                    visible.Add(s.Id);
                    firstMatch ??= s;
                    any = true;
                    break;
            }
        }
        return any;
    }

    private static void AddSubtree(Group group, HashSet<Guid> visible)
    {
        foreach (var child in group.Children)
        {
            visible.Add(child.Id);
            if (child is Group g)
                AddSubtree(g, visible);
        }
    }
}

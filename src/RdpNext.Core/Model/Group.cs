using System.Text.Json;
using System.Text.Json.Serialization;

namespace RdpNext.Core.Model;

public sealed class Group : Node
{
    private readonly List<Node> _children = [];

    public string? Username { get; set; }

    public string? Domain { get; set; }

    public bool IsExpanded { get; set; } = true;

    [JsonIgnore]
    public IReadOnlyList<Node> Children => _children;

    [JsonInclude]
    [JsonPropertyName("children")]
    private List<Node> SerializedChildren
    {
        get => _children;
        set
        {
            if (value is null || value.Contains(null!))
                throw new JsonException("Grup çocukları boş olamaz.");
            foreach (var child in value)
                Add(child);
        }
    }

    public void Add(Node child)
    {
        if (child is Group group && IsInside(group))
            throw new InvalidOperationException("Klasör kendi içine taşınamaz.");
        child.Parent?.Remove(child);
        _children.Add(child);
        child.Parent = this;
    }

    // Bu klasör verilen klasörün kendisi ya da altında mı?
    private bool IsInside(Group group)
    {
        for (Group? current = this; current is not null; current = current.Parent)
            if (current == group)
                return true;
        return false;
    }

    public void Remove(Node child)
    {
        if (_children.Remove(child))
            child.Parent = null;
    }

    public IEnumerable<Server> AllServers()
    {
        foreach (var child in _children)
        {
            if (child is Server server)
                yield return server;
            else if (child is Group group)
                foreach (var nested in group.AllServers())
                    yield return nested;
        }
    }
}

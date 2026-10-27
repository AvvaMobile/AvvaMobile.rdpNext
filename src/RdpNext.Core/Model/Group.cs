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
            foreach (var child in value)
                Add(child);
        }
    }

    public void Add(Node child)
    {
        child.Parent?.Remove(child);
        _children.Add(child);
        child.Parent = this;
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

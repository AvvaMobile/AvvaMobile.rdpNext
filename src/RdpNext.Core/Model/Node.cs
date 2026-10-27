using System.Text.Json.Serialization;

namespace RdpNext.Core.Model;

public abstract class Node
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; set; } = "";

    [JsonIgnore]
    public Group? Parent { get; internal set; }
}

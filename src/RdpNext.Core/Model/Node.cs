using System.Text.Json.Serialization;

namespace RdpNext.Core.Model;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(Group), "group")]
[JsonDerivedType(typeof(Server), "server")]
public abstract class Node
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; set; } = "";

    [JsonIgnore]
    public Group? Parent { get; internal set; }
}

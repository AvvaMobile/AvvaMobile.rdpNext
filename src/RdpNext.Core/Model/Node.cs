using System.Text.Json.Serialization;

namespace RdpNext.Core.Model;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(Group), "group")]
[JsonDerivedType(typeof(Server), "server")]
public abstract class Node
{
    public Guid Id { get; init; } = Guid.NewGuid();

    private string _name = "";

    public string Name
    {
        get => _name;
        set => _name = value ?? "";
    }

    [JsonIgnore]
    public Group? Parent { get; internal set; }
}

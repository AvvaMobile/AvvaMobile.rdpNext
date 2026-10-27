using CommunityToolkit.Mvvm.ComponentModel;
using RdpNext.Core.Model;

namespace RdpNext.App.ViewModels;

public abstract partial class TreeNodeViewModel(Node model) : ObservableObject
{
    public Node Model { get; } = model;

    public string Name => Model.Name;

    [ObservableProperty]
    private bool _isVisible = true;

    // Tek bir stil bağlaması için tabanda; yalnız gruplarda anlamlı.
    [ObservableProperty]
    private bool _isExpanded;

    partial void OnIsExpandedChanged(bool value) => ExpandedChanged(value);

    protected virtual void ExpandedChanged(bool value)
    {
    }

    public static TreeNodeViewModel Create(Node node) => node switch
    {
        Group group => new GroupNodeViewModel(group),
        Server server => new ServerNodeViewModel(server),
        _ => throw new ArgumentOutOfRangeException(nameof(node)),
    };
}

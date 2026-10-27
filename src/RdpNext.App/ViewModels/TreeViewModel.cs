using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using RdpNext.Core.Model;
using RdpNext.Core.Search;

namespace RdpNext.App.ViewModels;

public sealed partial class TreeViewModel : ObservableObject
{
    private readonly List<TreeNodeViewModel> _all = [];
    private readonly Dictionary<Guid, ServerNodeViewModel> _servers = [];

    public TreeViewModel(Group root)
    {
        Root = root;
        Roots = new(root.Children.Select(TreeNodeViewModel.Create));
        Index(Roots);
    }

    public Group Root { get; }

    public ObservableCollection<TreeNodeViewModel> Roots { get; }

    [ObservableProperty]
    private string _query = "";

    [ObservableProperty]
    private ServerNodeViewModel? _firstMatch;

    partial void OnQueryChanged(string value)
    {
        var result = ConnectionFilter.Apply(Root, value);
        var filtering = !string.IsNullOrWhiteSpace(value);
        foreach (var node in _all)
        {
            node.IsVisible = result.Visible.Contains(node.Model.Id);
            // Arama sırasında açılan gruplar modele yazılmaz; arama bitince eski hâline döner.
            if (node is GroupNodeViewModel group)
                group.ShowExpanded(filtering ? node.IsVisible : group.Group.IsExpanded);
        }
        FirstMatch = result.FirstMatch is { } server ? _servers[server.Id] : null;
    }

    private void Index(IEnumerable<TreeNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            _all.Add(node);
            if (node is ServerNodeViewModel server)
                _servers[server.Server.Id] = server;
            else if (node is GroupNodeViewModel group)
                Index(group.Children);
        }
    }
}

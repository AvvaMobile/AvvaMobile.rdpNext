using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using RdpNext.Core.Model;
using RdpNext.Core.Search;

namespace RdpNext.App.ViewModels;

public sealed partial class TreeViewModel : ObservableObject
{
    private readonly List<TreeNodeViewModel> _all = [];
    private readonly Dictionary<Guid, TreeNodeViewModel> _byId = [];

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

    [ObservableProperty]
    private TreeNodeViewModel? _selected;

    public T Node<T>(Guid id) where T : TreeNodeViewModel => (T)_byId[id];

    public IReadOnlyList<FolderOption> FolderOptions(Group? exclude = null)
    {
        var options = new List<FolderOption> { new(null, Strings.Strings.TopLevel) };
        AddOptions(Root, 0);
        return options;

        void AddOptions(Group parent, int depth)
        {
            foreach (var group in parent.Children.OfType<Group>())
            {
                if (group == exclude)
                    continue;
                options.Add(new FolderOption(group, new string(' ', depth * 4) + group.Name));
                AddOptions(group, depth + 1);
            }
        }
    }

    public ServerNodeViewModel AddServer(Server server, Group? parent) => (ServerNodeViewModel)AddNode(server, parent);

    public GroupNodeViewModel AddGroup(Group group, Group? parent) => (GroupNodeViewModel)AddNode(group, parent);

    public void Move(TreeNodeViewModel node, Group? newParent)
    {
        ChildrenOf(node.Model.Parent).Remove(node);
        (newParent ?? Root).Add(node.Model);
        ChildrenOf(newParent).Add(node);
        OnQueryChanged(Query);
    }

    public void Remove(TreeNodeViewModel node)
    {
        ChildrenOf(node.Model.Parent).Remove(node);
        node.Model.Parent?.Remove(node.Model);
        Unindex(node);
        if (Selected == node)
            Selected = null;
    }

    private TreeNodeViewModel AddNode(Node model, Group? parent)
    {
        (parent ?? Root).Add(model);
        var node = TreeNodeViewModel.Create(model);
        ChildrenOf(parent).Add(node);
        Index([node]);
        OnQueryChanged(Query);
        return node;
    }

    private ObservableCollection<TreeNodeViewModel> ChildrenOf(Group? group) =>
        group is null || group == Root ? Roots : Node<GroupNodeViewModel>(group.Id).Children;

    private void Unindex(TreeNodeViewModel node)
    {
        _all.Remove(node);
        _byId.Remove(node.Model.Id);
        if (node is GroupNodeViewModel group)
            foreach (var child in group.Children)
                Unindex(child);
    }

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
        FirstMatch = result.FirstMatch is { } server ? Node<ServerNodeViewModel>(server.Id) : null;
    }

    private void Index(IEnumerable<TreeNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            _all.Add(node);
            _byId[node.Model.Id] = node;
            if (node is GroupNodeViewModel group)
                Index(group.Children);
        }
    }
}

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

    // Ağaçta ekleme, taşıma ya da silme olunca; açık düzenleme sekmeleri klasör listesini yeniler.
    public event Action? StructureChanged;

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

    public void Move(TreeNodeViewModel node, Group? newParent) => MoveTo(node, newParent, null);

    // Öğeyi klasöre taşır, verilen kardeşin önüne (null: sona) koyar.
    public void MoveTo(TreeNodeViewModel node, Group? newParent, TreeNodeViewModel? before)
    {
        if (before == node)
            return;
        var oldParent = node.Model.Parent;
        var selected = Selected;
        // Önce model: klasör kendi içine taşınmak istenirse burada durur, ekran bozulmaz.
        (newParent ?? Root).Insert(node.Model, before?.Model);
        ChildrenOf(oldParent).Remove(node);
        var siblings = ChildrenOf(newParent);
        siblings.Insert(before is null ? siblings.Count : siblings.IndexOf(before), node);
        Selected = selected;
        OnQueryChanged(Query);
        StructureChanged?.Invoke();
    }

    // Sürükle-bırak kuralları: klasörler yalnız en üstte sıralanır, sunucular her yere gider.
    public DropTarget? ResolveDrop(TreeNodeViewModel dragged, TreeNodeViewModel target, DropPlace place)
    {
        if (dragged == target)
            return null;
        var targetParent = target.Model.Parent == Root ? null : target.Model.Parent;
        if (dragged is GroupNodeViewModel && (targetParent is not null || place == DropPlace.Into))
            return null;
        if (dragged is ServerNodeViewModel && target is GroupNodeViewModel group)
        {
            // Klasörün üstüne bırakılan sunucu klasörün en üstüne girer.
            if (place == DropPlace.Into)
                return new(group.Group, group.Children.FirstOrDefault(), true);
            // Açık klasörün alt kenarı, ilk sunucusunun hemen üstüdür.
            if (place == DropPlace.Below && group.IsExpanded && group.Children.Count > 0)
                return new(group.Group, group.Children[0], false);
        }
        return new(targetParent, place == DropPlace.Above ? target : NextSibling(target), false);
    }

    private TreeNodeViewModel? NextSibling(TreeNodeViewModel node)
    {
        var siblings = ChildrenOf(node.Model.Parent);
        var index = siblings.IndexOf(node) + 1;
        return index < siblings.Count ? siblings[index] : null;
    }

    // Bütün listeyi verilen klasörün çocuklarıyla değiştirir (içe aktarma).
    public void Replace(Group source)
    {
        Selected = null;
        foreach (var child in Root.Children.ToList())
            Root.Remove(child);
        foreach (var child in source.Children.ToList())
            Root.Add(child);
        Roots.Clear();
        _all.Clear();
        _byId.Clear();
        foreach (var child in Root.Children)
            Roots.Add(TreeNodeViewModel.Create(child));
        Index(Roots);
        OnQueryChanged(Query);
        StructureChanged?.Invoke();
    }

    // Dosyadaki kayıtları listeye ekler; listede zaten olan kayıt (aynı kimlik) atlanır,
    // aynı klasörün içindeki yeni kayıtlar o klasöre eklenir. Eklenen sunucu sayısını döner.
    public int Merge(Group source)
    {
        var added = 0;
        MergeInto(null, source);
        return added;

        void MergeInto(Group? target, Group from)
        {
            foreach (var child in from.Children.ToList())
            {
                if (!_byId.TryGetValue(child.Id, out var existing))
                {
                    added += child is Group group ? group.AllServers().Count() : 1;
                    AddNode(child, target);
                }
                else if (existing is GroupNodeViewModel folder && child is Group group)
                    MergeInto(folder.Group, group);
            }
        }
    }

    public void Remove(TreeNodeViewModel node)
    {
        if (Selected is { } selected && IsUnder(selected.Model, node.Model))
            Selected = null;
        ChildrenOf(node.Model.Parent).Remove(node);
        node.Model.Parent?.Remove(node.Model);
        Unindex(node);
        OnQueryChanged(Query);
        StructureChanged?.Invoke();
    }

    // Klasör hâlâ ağaçta mı (silinmiş bir klasörün altında değil mi)?
    public bool IsAttached(Group? group)
    {
        for (var current = group; current is not null; current = current.Parent)
            if (current == Root)
                return true;
        return group is null;
    }

    private static bool IsUnder(Node node, Node ancestor)
    {
        for (Node? current = node; current is not null; current = current.Parent)
            if (current == ancestor)
                return true;
        return false;
    }

    private TreeNodeViewModel AddNode(Node model, Group? parent)
    {
        (parent ?? Root).Add(model);
        var node = TreeNodeViewModel.Create(model);
        ChildrenOf(parent).Add(node);
        Index([node]);
        OnQueryChanged(Query);
        StructureChanged?.Invoke();
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

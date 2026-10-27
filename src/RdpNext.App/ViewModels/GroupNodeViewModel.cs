using System.Collections.ObjectModel;
using RdpNext.Core.Model;

namespace RdpNext.App.ViewModels;

public sealed partial class GroupNodeViewModel : TreeNodeViewModel
{
    public GroupNodeViewModel(Group group) : base(group)
    {
        Group = group;
        Children = new(group.Children.Select(Create));
        ServerCount = group.AllServers().Count();
        IsExpanded = group.IsExpanded;
    }

    public Group Group { get; }

    public ObservableCollection<TreeNodeViewModel> Children { get; }

    public int ServerCount { get; }

    private bool _showingFilter;

    public void ShowExpanded(bool value)
    {
        _showingFilter = true;
        IsExpanded = value;
        _showingFilter = false;
    }

    protected override void ExpandedChanged(bool value)
    {
        if (!_showingFilter)
            Group.IsExpanded = value;
    }
}

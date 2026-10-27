using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using RdpNext.Core.Model;

namespace RdpNext.App.ViewModels;

public sealed partial class TreeViewModel(Group root) : ObservableObject
{
    public Group Root { get; } = root;

    public ObservableCollection<TreeNodeViewModel> Roots { get; } = new(root.Children.Select(TreeNodeViewModel.Create));
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RdpNext.App.ViewModels;

public sealed partial class SessionsViewModel : ObservableObject
{
    public ObservableCollection<SessionTabViewModel> Tabs { get; } = [];

    [ObservableProperty]
    private SessionTabViewModel? _selected;

    public SessionTabViewModel Open(ServerNodeViewModel server)
    {
        var tab = Tabs.FirstOrDefault(t => t.Server == server);
        if (tab is null)
        {
            tab = new SessionTabViewModel(server);
            Tabs.Add(tab);
            server.IsConnected = true;
        }
        Selected = tab;
        return tab;
    }

    public void Close(SessionTabViewModel tab)
    {
        var index = Tabs.IndexOf(tab);
        if (index < 0)
            return;
        Tabs.RemoveAt(index);
        tab.Server.IsConnected = false;
        if (Selected == tab)
            Selected = Tabs.Count == 0 ? null : Tabs[Math.Max(index - 1, 0)];
    }

    public void CloseOthers(SessionTabViewModel keep)
    {
        foreach (var tab in Tabs.Where(t => t != keep).ToList())
            Close(tab);
        Selected = keep;
    }
}

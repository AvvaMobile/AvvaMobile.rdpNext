using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RdpNext.App.ViewModels;

public sealed partial class SessionsViewModel : ObservableObject
{
    public ObservableCollection<TabViewModel> Tabs { get; } = [];

    [ObservableProperty]
    private TabViewModel? _selected;

    public SessionTabViewModel Open(ServerNodeViewModel server)
    {
        var tab = Find<SessionTabViewModel>(t => t.Server == server);
        if (tab is null)
        {
            tab = new SessionTabViewModel(server);
            Tabs.Add(tab);
            server.IsConnected = true;
        }
        Selected = tab;
        return tab;
    }

    public void OpenTab(TabViewModel tab)
    {
        if (!Tabs.Contains(tab))
            Tabs.Add(tab);
        Selected = tab;
    }

    public T? Find<T>(Func<T, bool> match) where T : TabViewModel =>
        Tabs.OfType<T>().FirstOrDefault(match);

    public void Close(TabViewModel tab)
    {
        var index = Tabs.IndexOf(tab);
        if (index < 0)
            return;
        // Sekme listeden çıkınca ekrandaki ListBox seçimi null yapabilir; önce bakılır.
        var wasSelected = Selected == tab;
        Tabs.RemoveAt(index);
        tab.OnClosed();
        if (wasSelected)
            Selected = Tabs.Count == 0 ? null : Tabs[Math.Max(index - 1, 0)];
    }

    public void CloseOthers(TabViewModel keep)
    {
        foreach (var tab in Tabs.Where(t => t != keep).ToList())
            Close(tab);
        Selected = keep;
    }
}

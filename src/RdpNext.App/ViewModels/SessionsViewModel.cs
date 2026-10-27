using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RdpNext.App.ViewModels;

public sealed partial class SessionsViewModel(TimeProvider? time = null) : ObservableObject
{
    public ObservableCollection<TabViewModel> Tabs { get; } = [];

    // Tam ekrandaki oturumlar: her biri kendi penceresinde açılır, sekme şeridinde durmaz.
    public ObservableCollection<SessionTabViewModel> Windows { get; } = [];

    [ObservableProperty]
    private TabViewModel? _selected;

    public SessionTabViewModel Open(ServerNodeViewModel server)
    {
        var tab = Find<SessionTabViewModel>(t => t.Server == server);
        if (tab is not null && Windows.Contains(tab))
            return tab;
        if (tab is null)
        {
            tab = new SessionTabViewModel(server, time);
            Tabs.Add(tab);
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

    // Sekmeyi verilen sıraya taşır (sürükle-bırak); seçim değişmez.
    public void MoveTab(TabViewModel tab, int index)
    {
        var from = Tabs.IndexOf(tab);
        index = Math.Clamp(index, 0, Tabs.Count - 1);
        if (from < 0 || from == index)
            return;
        var selected = Selected;
        Tabs.Move(from, index);
        Selected = selected;
    }

    public T? Find<T>(Func<T, bool> match) where T : TabViewModel =>
        Tabs.OfType<T>().Concat(Windows.OfType<T>()).FirstOrDefault(match);

    // Oturum sekme şeridinden çıkar, kendi tam ekran penceresine geçer; oturum kapanmaz.
    public void Detach(SessionTabViewModel tab)
    {
        var index = Tabs.IndexOf(tab);
        if (index < 0)
            return;
        var wasSelected = Selected == tab;
        Tabs.RemoveAt(index);
        if (wasSelected)
            Selected = Tabs.Count == 0 ? null : Tabs[Math.Max(index - 1, 0)];
        Windows.Add(tab);
    }

    // Tam ekrandan çıkan oturum sekmesine döner.
    public void Attach(SessionTabViewModel tab)
    {
        if (!Windows.Remove(tab))
            return;
        Tabs.Add(tab);
        Selected = tab;
    }

    public void Close(TabViewModel tab)
    {
        if (tab is SessionTabViewModel session && Windows.Remove(session))
        {
            tab.OnClosed();
            return;
        }
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

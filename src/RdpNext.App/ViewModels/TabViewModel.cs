using CommunityToolkit.Mvvm.ComponentModel;
using RdpNext.Core.Model;

namespace RdpNext.App.ViewModels;

// Sekme şeridindeki her sekme: oturum ya da düzenleme ekranı.
public abstract class TabViewModel : ObservableObject
{
    public abstract string Title { get; }

    // Oturum sekmesinde sunucunun rengi; düzenleme sekmesinde null (dişli ikonu).
    public virtual ServerColor? Color => null;

    public virtual ServerIcon Icon => ServerIcon.Windows;

    public bool IsEditor => Color is null;

    public virtual void OnClosed()
    {
    }

    protected void TitleChanged() => OnPropertyChanged(nameof(Title));
}

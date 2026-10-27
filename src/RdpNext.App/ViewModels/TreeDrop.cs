using RdpNext.Core.Model;

namespace RdpNext.App.ViewModels;

// Sürüklenen öğe hedef satırın neresine bırakıldı: üst kenarı, ortası (klasörün içi) ya da alt kenarı.
public enum DropPlace
{
    Above,
    Into,
    Below,
}

// Bırakılınca öğenin gideceği klasör (null: en üst) ve önüne gireceği kardeş (null: sona).
// Into: klasörün kendisine bırakıldı (en üstüne girer), ekranda klasör vurgulanır.
public sealed record DropTarget(Group? Parent, TreeNodeViewModel? Before, bool Into);

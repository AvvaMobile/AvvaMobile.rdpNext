using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using RdpNext.Rdp;

namespace RdpNext.App.ViewModels;

// Oturumun ekran görüntüsü. Kareler oturum iş parçacığından gelir, yalnız değişen dikdörtgen kopyalanır;
// arayüze bekleyen bir "yenile" yoksa tek bir tane gönderilir.
public sealed partial class SessionScreen : ObservableObject
{
    private WriteableBitmap? _bitmap;
    private int _invalidatePending;

    public WriteableBitmap? Bitmap => _bitmap;

    // Görüntü değişti; görünüm kendini yeniden çizer.
    public event Action? Invalidated;

    // Sekme kapanınca görüntü bırakılır (büyük ekranda onlarca MB).
    public void Clear()
    {
        var old = _bitmap;
        _bitmap = null;
        OnPropertyChanged(nameof(Bitmap));
        old?.Dispose();
    }

    public unsafe void CopyFrame(RdpFrame frame)
    {
        var bitmap = _bitmap;
        var (x, y, w, h) = (frame.X, frame.Y, frame.W, frame.H);
        var replaced = false;
        if (bitmap is null || bitmap.PixelSize.Width != frame.Width || bitmap.PixelSize.Height != frame.Height)
        {
            bitmap = new WriteableBitmap(new PixelSize(frame.Width, frame.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
            (x, y, w, h) = (0, 0, frame.Width, frame.Height);
            replaced = true;
        }
        x = Math.Clamp(x, 0, frame.Width);
        y = Math.Clamp(y, 0, frame.Height);
        w = Math.Clamp(w, 0, frame.Width - x);
        h = Math.Clamp(h, 0, frame.Height - y);
        using (var target = bitmap.Lock())
        {
            for (var row = y; row < y + h; row++)
                Buffer.MemoryCopy((byte*)frame.Buffer + row * frame.Stride + x * 4, (byte*)target.Address + row * target.RowBytes + x * 4,
                    target.RowBytes - x * 4, w * 4);
        }
        if (replaced)
        {
            var old = _bitmap;
            _bitmap = bitmap;
            Dispatcher.UIThread.Post(() =>
            {
                OnPropertyChanged(nameof(Bitmap));
                old?.Dispose();
            });
        }
        if (Interlocked.Exchange(ref _invalidatePending, 1) == 0)
            Dispatcher.UIThread.Post(() =>
            {
                _invalidatePending = 0;
                Invalidated?.Invoke();
            });
    }
}

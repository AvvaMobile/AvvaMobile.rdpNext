using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using RdpNext.App.Rdp;
using RdpNext.App.ViewModels;
using RdpNext.Rdp;

namespace RdpNext.App.Views;

// Oturumun görüntüsü ve girdisi. Sekme değişince görünüm yeniden kurulur; görüntü SessionScreen'de durduğu için kaybolmaz.
public partial class SessionView : UserControl
{
    private readonly KeyboardTranslator _keys = new(OperatingSystem.IsMacOS());
    private SessionTabViewModel? _tab;
    // Karakter olayı tuş değiştiricilerini taşımaz; son tuş olayındakiler kullanılır.
    private KeyModifiers _modifiers;

    public SessionView()
    {
        InitializeComponent();
        DetachedFromVisualTree += (_, _) => Watch(null);
        SizeChanged += (_, e) => _tab?.ViewSizeChanged(e.NewSize.Width, e.NewSize.Height);
        LostFocus += (_, _) => ReleaseKeys();
        // Oturuma dönülünce bu bilgisayarın panosundaki metin sunucuya duyurulur.
        GotFocus += async (_, _) =>
        {
            if (Session is { } session && _tab is { } tab && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard
                && await clipboard.TryGetTextAsync() is { } text && text != tab.SharedClipboardText)
            {
                tab.SharedClipboardText = text;
                session.SetLocalClipboardText(text);
            }
        };
    }

    private IRdpSession? Session => _tab?.Session;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Watch(DataContext as SessionTabViewModel);
    }

    private void Watch(SessionTabViewModel? tab)
    {
        if (_tab is not null)
        {
            _tab.Screen.Invalidated -= Redraw;
            _tab.PropertyChanged -= OnTabChanged;
            _tab.RemoteClipboardText -= OnRemoteClipboardText;
        }
        _tab = tab;
        if (_tab is not null)
        {
            _tab.Screen.Invalidated += Redraw;
            _tab.PropertyChanged += OnTabChanged;
            _tab.RemoteClipboardText += OnRemoteClipboardText;
            ShowPointer(_tab.Pointer);
        }
    }

    private void Redraw() => Screen.InvalidateVisual();

    // Sunucuda kopyalandı. Metinse Mac panosuna yazılır; metin dışıysa (dosya) Mac'teki metin "bilinen" sayılır,
    // böylece sonraki odakta sunucuya duyurulup sunucudaki kopyanın üstüne yazmaz.
    private async void OnRemoteClipboardText(string? text)
    {
        if (_tab is not { } tab || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
            return;
        if (text is null)
        {
            tab.SharedClipboardText = await clipboard.TryGetTextAsync();
            return;
        }
        tab.SharedClipboardText = text;
        await clipboard.SetTextAsync(text);
    }

    private void OnTabChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SessionTabViewModel.Pointer))
            ShowPointer(_tab?.Pointer);
    }

    private void ShowPointer(RdpPointer? pointer)
    {
        Cursor = pointer switch
        {
            null => Cursor.Default,
            { Width: 0 } => new Cursor(StandardCursorType.None),
            _ => new Cursor(ToBitmap(pointer), new PixelPoint(pointer.HotX, pointer.HotY)),
        };
    }

    private static unsafe Bitmap ToBitmap(RdpPointer pointer)
    {
        fixed (byte* data = pointer.Bgra)
            return new Bitmap(PixelFormat.Bgra8888, AlphaFormat.Unpremul, (IntPtr)data,
                new PixelSize(pointer.Width, pointer.Height), new Vector(96, 96), pointer.Width * 4);
    }

    // Görünümdeki nokta sunucu ekranındaki piksele; görüntü küçültülmüşse ölçeklenir.
    private (int X, int Y) Remote(PointerEventArgs e)
    {
        var p = e.GetPosition(Screen);
        if (_tab?.Screen.Bitmap is { } bitmap && Screen.Bounds.Width > 0 && Screen.Bounds.Height > 0)
            p = new Point(p.X * bitmap.PixelSize.Width / Screen.Bounds.Width, p.Y * bitmap.PixelSize.Height / Screen.Bounds.Height);
        return ((int)p.X, (int)p.Y);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = Remote(e);
        Session?.SendMouse(RdpMouseFlags.Move, (int)p.X, (int)p.Y);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        SendButton(e, e.GetCurrentPoint(Screen).Properties.PointerUpdateKind);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        SendButton(e, e.GetCurrentPoint(Screen).Properties.PointerUpdateKind);
        e.Handled = true;
    }

    private void SendButton(PointerEventArgs e, PointerUpdateKind kind)
    {
        var flags = kind switch
        {
            PointerUpdateKind.LeftButtonPressed => RdpMouseFlags.Down | RdpMouseFlags.Button1,
            PointerUpdateKind.RightButtonPressed => RdpMouseFlags.Down | RdpMouseFlags.Button2,
            PointerUpdateKind.MiddleButtonPressed => RdpMouseFlags.Down | RdpMouseFlags.Button3,
            PointerUpdateKind.LeftButtonReleased => RdpMouseFlags.Button1,
            PointerUpdateKind.RightButtonReleased => RdpMouseFlags.Button2,
            PointerUpdateKind.MiddleButtonReleased => RdpMouseFlags.Button3,
            _ => (RdpMouseFlags?)null,
        };
        if (flags is not { } f)
            return;
        var p = Remote(e);
        Session?.SendMouse(f, (int)p.X, (int)p.Y);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var p = Remote(e);
        if (e.Delta.Y != 0)
            Session?.SendWheel((int)Math.Round(e.Delta.Y * 120), false, (int)p.X, (int)p.Y);
        if (e.Delta.X != 0)
            Session?.SendWheel((int)Math.Round(-e.Delta.X * 120), true, (int)p.X, (int)p.Y);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        _modifiers = e.KeyModifiers;
        if (Session is not null && _keys.KeyDown(e.PhysicalKey, e.KeyModifiers) is { } actions)
        {
            Send(actions);
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        _modifiers = e.KeyModifiers;
        if (Session is not null)
        {
            Send(_keys.KeyUp(e.PhysicalKey, e.KeyModifiers));
            e.Handled = true;
        }
        base.OnKeyUp(e);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (Session is not null && e.Text is { } text)
        {
            Send(_keys.TextInput(text, _modifiers));
            e.Handled = true;
        }
        base.OnTextInput(e);
    }

    // Sunucuda basılı kalmış tuşları bırakır.
    public void ReleaseKeys() => Send(_keys.ReleaseAll());

    private void Send(IReadOnlyList<KeyAction> actions)
    {
        foreach (var action in actions)
        {
            switch (action)
            {
                case ScancodeAction s:
                    Session?.SendKey(s.Code, s.Extended, s.Down);
                    break;
                case UnicodeAction u:
                    Session?.SendUnicode(u.Char);
                    break;
            }
        }
    }
}

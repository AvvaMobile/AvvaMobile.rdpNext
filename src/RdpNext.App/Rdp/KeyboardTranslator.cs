using Avalonia.Input;

namespace RdpNext.App.Rdp;

public abstract record KeyAction;

public sealed record ScancodeAction(ushort Code, bool Extended, bool Down) : KeyAction;

public sealed record UnicodeAction(char Char) : KeyAction;

// Klavye olaylarını sunucuya gidecek tuşlara çevirir.
// Mac'te yazı tuşları Mac'in ürettiği karakter olarak (Unicode) gider, böylece Türkçe Mac düzeni sunucunun
// düzeninden bağımsız çalışır; Ctrl'li kısayollar, ok, F tuşları gibi diğer tuşlar scancode olarak gider.
// Option tek başına gönderilmez (Option+Q ile @ yazarken Windows menü çubuğu açılmasın); Option basılıyken
// yazı olmayan bir tuşa basılırsa (Option+F4) Alt o an gönderilir. Cmd+C/V/X/A/Z sunucuya Ctrl ile gider.
// Windows'ta mstsc gibi her tuş scancode olarak gider.
public sealed class KeyboardTranslator(bool mac)
{
    private static readonly HashSet<PhysicalKey> CmdShortcuts = [PhysicalKey.C, PhysicalKey.V, PhysicalKey.X, PhysicalKey.A, PhysicalKey.Z];
    private static readonly ScancodeAction CtrlDown = new(0x1D, false, true);
    private static readonly ScancodeAction CtrlUp = new(0x1D, false, false);

    private bool _altHeld;
    private bool _altSent;

    // null: tuş işlenmiş sayılmaz (uygulamaya ya da karakter olayına kalır).
    public IReadOnlyList<KeyAction>? KeyDown(PhysicalKey key, KeyModifiers modifiers)
    {
        if (!mac)
            return Scancode(key, down: true) is { } action ? [action] : null;

        if (key is PhysicalKey.MetaLeft or PhysicalKey.MetaRight or PhysicalKey.CapsLock)
            return [];
        if (key is PhysicalKey.AltLeft or PhysicalKey.AltRight)
        {
            _altHeld = true;
            return [];
        }
        if (modifiers.HasFlag(KeyModifiers.Meta))
            return CmdShortcuts.Contains(key) && Scancode(key, down: true) is { } press
                ? [CtrlDown, press, press with { Down = false }, CtrlUp]
                : null;
        // İşlenmemiş bırakılır ki macOS karakter olayını (TextInput) üretsin.
        if (KeyMap.IsText(key) && !modifiers.HasFlag(KeyModifiers.Control))
            return null;
        if (Scancode(key, down: true) is not { } scancode)
            return null;
        if (_altHeld && !_altSent)
        {
            _altSent = true;
            return [new ScancodeAction(0x38, false, true), scancode];
        }
        return [scancode];
    }

    public IReadOnlyList<KeyAction> KeyUp(PhysicalKey key, KeyModifiers modifiers)
    {
        if (!mac)
            return Scancode(key, down: false) is { } action ? [action] : [];

        if (key is PhysicalKey.MetaLeft or PhysicalKey.MetaRight or PhysicalKey.CapsLock)
            return [];
        if (key is PhysicalKey.AltLeft or PhysicalKey.AltRight)
        {
            var sent = _altSent;
            _altHeld = _altSent = false;
            return sent ? [new ScancodeAction(0x38, false, false)] : [];
        }
        if (modifiers.HasFlag(KeyModifiers.Meta) || (KeyMap.IsText(key) && !modifiers.HasFlag(KeyModifiers.Control)))
            return [];
        return Scancode(key, down: false) is { } scancode ? [scancode] : [];
    }

    public IReadOnlyList<KeyAction> TextInput(string text, KeyModifiers modifiers) =>
        !mac || modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta)
            ? []
            : text.Where(c => !char.IsControl(c)).Select(c => (KeyAction)new UnicodeAction(c)).ToList();

    private static ScancodeAction? Scancode(PhysicalKey key, bool down) =>
        KeyMap.Scancode(key) is { } code ? new ScancodeAction(code.Code, code.Extended, down) : null;
}

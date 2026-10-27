using Avalonia.Input;
using RdpNext.App.Rdp;

namespace RdpNext.App.Tests;

public class KeyMapTests
{
    private static ScancodeAction Sc(ushort code, bool down, bool extended = false) => new(code, extended, down);

    [Theory]
    [InlineData(PhysicalKey.A, 0x1E)]
    [InlineData(PhysicalKey.Z, 0x2C)]
    [InlineData(PhysicalKey.Q, 0x10)]
    [InlineData(PhysicalKey.Digit1, 0x02)]
    [InlineData(PhysicalKey.F12, 0x58)]
    public void LettersMapToScancodes(PhysicalKey key, int code) =>
        Assert.Equal(((ushort)code, false), KeyMap.Scancode(key));

    [Fact]
    public void ArrowsAreExtended() => Assert.Equal(((ushort)0x4B, true), KeyMap.Scancode(PhysicalKey.ArrowLeft));

    [Fact]
    public void EnterAndNumpadEnterDiffer()
    {
        Assert.Equal(((ushort)0x1C, false), KeyMap.Scancode(PhysicalKey.Enter));
        Assert.Equal(((ushort)0x1C, true), KeyMap.Scancode(PhysicalKey.NumPadEnter));
    }

    [Fact]
    public void CmdCSendsCtrlC()
    {
        var keys = new KeyboardTranslator(mac: true);
        Assert.Empty(keys.KeyDown(PhysicalKey.MetaLeft, KeyModifiers.Meta)!);

        Assert.Equal([Sc(0x1D, true), Sc(0x2E, true), Sc(0x2E, false), Sc(0x1D, false)],
            keys.KeyDown(PhysicalKey.C, KeyModifiers.Meta)!);
        Assert.Empty(keys.KeyUp(PhysicalKey.C, KeyModifiers.Meta));
    }

    // Diğer Cmd kısayolları uygulamada kalır (Cmd+Q çıkış gibi).
    [Fact]
    public void CmdQIsNotSent() => Assert.Null(new KeyboardTranslator(mac: true).KeyDown(PhysicalKey.Q, KeyModifiers.Meta));

    // Mac'te yazı, Mac'in ürettiği karakter olarak gider; sunucunun klavye düzeni fark etmez.
    // Yazı tuşunun KeyDown'u işlenmiş sayılmaz: macOS işlenmiş tuştan karakter olayı üretmiyor.
    [Fact]
    public void TextGoesAsUnicodeOnMac()
    {
        var keys = new KeyboardTranslator(mac: true);
        Assert.Null(keys.KeyDown(PhysicalKey.I, KeyModifiers.None));
        Assert.Equal([new UnicodeAction('ı')], keys.TextInput("ı", KeyModifiers.None));
        Assert.Empty(keys.KeyUp(PhysicalKey.I, KeyModifiers.None));
    }

    // Option+Q (@) gibi karakterler Alt basılı gitmez; yoksa Windows menü çubuğunu açar.
    [Fact]
    public void OptionWithTextSendsNoAlt()
    {
        var keys = new KeyboardTranslator(mac: true);
        Assert.Empty(keys.KeyDown(PhysicalKey.AltLeft, KeyModifiers.Alt)!);
        Assert.Null(keys.KeyDown(PhysicalKey.Q, KeyModifiers.Alt));
        Assert.Equal([new UnicodeAction('@')], keys.TextInput("@", KeyModifiers.Alt));
        Assert.Empty(keys.KeyUp(PhysicalKey.AltLeft, KeyModifiers.None));
    }

    [Fact]
    public void OptionWithF4SendsAlt()
    {
        var keys = new KeyboardTranslator(mac: true);
        keys.KeyDown(PhysicalKey.AltLeft, KeyModifiers.Alt);

        Assert.Equal([Sc(0x38, true), Sc(0x3E, true)], keys.KeyDown(PhysicalKey.F4, KeyModifiers.Alt)!);
        Assert.Equal([Sc(0x38, false)], keys.KeyUp(PhysicalKey.AltLeft, KeyModifiers.None));
    }

    [Fact]
    public void ControlLetterIsScancodeOnMac()
    {
        var keys = new KeyboardTranslator(mac: true);
        Assert.Equal([Sc(0x1D, true)], keys.KeyDown(PhysicalKey.ControlLeft, KeyModifiers.Control)!);
        Assert.Equal([Sc(0x2C, true)], keys.KeyDown(PhysicalKey.Z, KeyModifiers.Control)!);
        Assert.Empty(keys.TextInput("z", KeyModifiers.Control));
    }

    // Windows'ta mstsc gibi her tuş scancode olarak gider.
    [Fact]
    public void WindowsSendsLettersAsScancodes()
    {
        var keys = new KeyboardTranslator(mac: false);
        Assert.Equal([Sc(0x1E, true)], keys.KeyDown(PhysicalKey.A, KeyModifiers.None)!);
        Assert.Empty(keys.TextInput("a", KeyModifiers.None));
        Assert.Equal([Sc(0x1E, false)], keys.KeyUp(PhysicalKey.A, KeyModifiers.None));
    }
}

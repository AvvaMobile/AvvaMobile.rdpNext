using System.Runtime.Versioning;
using RdpNext.Core.Secrets;

namespace RdpNext.Core.Tests;

// Gerçek giriş anahtar zincirine yazar; her test kendi kaydını siler.
[SupportedOSPlatform("macos")]
public sealed class MacKeychainSecretStoreTests : IDisposable
{
    private readonly MacKeychainSecretStore _store = new("rdpNext-test-" + Guid.NewGuid());
    private readonly List<Guid> _ids = [];

    private Guid NewId()
    {
        var id = Guid.NewGuid();
        _ids.Add(id);
        return id;
    }

    public void Dispose()
    {
        if (OperatingSystem.IsMacOS())
            foreach (var id in _ids)
                _store.DeletePassword(id);
    }

    [Fact]
    public void SetThenGetReturnsPassword()
    {
        Assert.SkipUnless(OperatingSystem.IsMacOS(), "yalnız macOS");
        var id = NewId();

        _store.SetPassword(id, "Gizli123!");

        Assert.Equal("Gizli123!", _store.GetPassword(id));
    }

    [Fact]
    public void SetTwiceOverwrites()
    {
        Assert.SkipUnless(OperatingSystem.IsMacOS(), "yalnız macOS");
        var id = NewId();

        _store.SetPassword(id, "ilk");
        _store.SetPassword(id, "ikinci");

        Assert.Equal("ikinci", _store.GetPassword(id));
    }

    [Fact]
    public void MissingGivesNull()
    {
        Assert.SkipUnless(OperatingSystem.IsMacOS(), "yalnız macOS");

        Assert.Null(_store.GetPassword(NewId()));
    }

    [Fact]
    public void DeleteRemoves()
    {
        Assert.SkipUnless(OperatingSystem.IsMacOS(), "yalnız macOS");
        var id = NewId();
        _store.SetPassword(id, "silinecek");

        _store.DeletePassword(id);

        Assert.Null(_store.GetPassword(id));
    }

    [Fact]
    public void DeleteMissingDoesNotThrow()
    {
        Assert.SkipUnless(OperatingSystem.IsMacOS(), "yalnız macOS");

        _store.DeletePassword(NewId());
    }

    [Fact]
    public void UnicodePasswordRoundTrips()
    {
        Assert.SkipUnless(OperatingSystem.IsMacOS(), "yalnız macOS");
        var id = NewId();

        _store.SetPassword(id, "Şifre-ğüı€");

        Assert.Equal("Şifre-ğüı€", _store.GetPassword(id));
    }
}

using RdpNext.Core.Storage;

namespace RdpNext.Core.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rdpnext-tests-" + Guid.NewGuid());
    private readonly string _path;

    public SettingsStoreTests() => _path = Path.Combine(_dir, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void MissingFileGivesDefaults() => Assert.NotNull(new SettingsStore(_path).Load());

    [Fact]
    public void CorruptFileGivesDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_path, "{ bozuk");

        Assert.NotNull(new SettingsStore(_path).Load());
    }
}

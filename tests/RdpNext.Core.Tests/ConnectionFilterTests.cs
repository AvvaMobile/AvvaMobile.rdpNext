using System.Diagnostics;
using RdpNext.Core.Search;

namespace RdpNext.Core.Tests;

public class ConnectionFilterTests
{
    private readonly Group _root = new() { Name = "kök" };
    private readonly Group _uretim = new() { Name = "Üretim" };
    private readonly Group _test = new() { Name = "Test" };
    private readonly Group _musteriler = new() { Name = "Müşteriler" };
    private readonly Server _web01 = new() { Name = "web-01", Host = "10.0.1.11" };
    private readonly Server _sql01 = new() { Name = "sql-01", Host = "10.0.1.20" };
    private readonly Server _webTest = new() { Name = "web-test", Host = "10.0.2.11" };
    private readonly Server _tek = new() { Name = "tek-sunucu", Host = "192.168.1.5" };

    public ConnectionFilterTests()
    {
        _root.Add(_uretim);
        _root.Add(_test);
        _root.Add(_musteriler);
        _root.Add(_tek);
        _uretim.Add(_web01);
        _uretim.Add(_sql01);
        _test.Add(_webTest);
    }

    private Node[] AllNodes => [_root, _uretim, _test, _musteriler, _web01, _sql01, _webTest, _tek];

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyQueryShowsEverythingAndNoFirstMatch(string? query)
    {
        var result = ConnectionFilter.Apply(_root, query);

        Assert.All(AllNodes, n => Assert.Contains(n.Id, result.Visible));
        Assert.Null(result.FirstMatch);
    }

    [Fact]
    public void MatchesServerByName()
    {
        var result = ConnectionFilter.Apply(_root, "web");

        Assert.Contains(_web01.Id, result.Visible);
        Assert.Contains(_webTest.Id, result.Visible);
        Assert.Contains(_uretim.Id, result.Visible);
        Assert.Contains(_test.Id, result.Visible);
        Assert.DoesNotContain(_sql01.Id, result.Visible);
        Assert.DoesNotContain(_musteriler.Id, result.Visible);
        Assert.DoesNotContain(_tek.Id, result.Visible);
    }

    [Fact]
    public void MatchesServerByHost()
    {
        var result = ConnectionFilter.Apply(_root, "10.0.1.2");

        Assert.Contains(_sql01.Id, result.Visible);
        Assert.Contains(_uretim.Id, result.Visible);
        Assert.DoesNotContain(_web01.Id, result.Visible);
        Assert.DoesNotContain(_test.Id, result.Visible);
    }

    [Fact]
    public void GroupNameMatchShowsWholeGroup()
    {
        var musteri = ConnectionFilter.Apply(_root, "musteri");
        Assert.Contains(_musteriler.Id, musteri.Visible);
        Assert.DoesNotContain(_uretim.Id, musteri.Visible);

        var uretim = ConnectionFilter.Apply(_root, "uretim");
        Assert.Contains(_uretim.Id, uretim.Visible);
        Assert.Contains(_web01.Id, uretim.Visible);
        Assert.Contains(_sql01.Id, uretim.Visible);
        Assert.DoesNotContain(_webTest.Id, uretim.Visible);
    }

    [Fact]
    public void FirstMatchIsFirstVisibleServerInTreeOrder() =>
        Assert.Same(_web01, ConnectionFilter.Apply(_root, "web").FirstMatch);

    [Fact]
    public void FiveThousandServersFilterUnder100ms()
    {
        var root = new Group { Name = "kök" };
        for (var g = 0; g < 50; g++)
        {
            var group = new Group { Name = $"Grup {g}" };
            root.Add(group);
            for (var s = 0; s < 100; s++)
                group.Add(new Server { Name = $"sunucu-{g:00}-{s:00}", Host = $"10.{g}.0.{s}" });
        }
        ConnectionFilter.Apply(root, "isinma");

        var watch = Stopwatch.StartNew();
        var result = ConnectionFilter.Apply(root, "sunucu-49-99");
        watch.Stop();

        Assert.Equal("sunucu-49-99", result.FirstMatch?.Name);
        Assert.True(watch.ElapsedMilliseconds < 100, $"{watch.ElapsedMilliseconds} ms");
    }
}

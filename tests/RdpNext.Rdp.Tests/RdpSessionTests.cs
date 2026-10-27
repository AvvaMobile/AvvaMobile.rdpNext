using System.Diagnostics;

namespace RdpNext.Rdp.Tests;

// Gerçek FreeRDP ile; kapalı port ve ulaşılmaz adrese bağlanır, sunucu gerekmez.
public class RdpSessionTests
{
    private static RdpConnectOptions To(string host, int port) => new(host, port, "user", null, "pw", 1280, 800);

    [Fact]
    public async Task ConnectToClosedPortEndsWithConnectFailed()
    {
        Assert.SkipUnless(RdpSession.IsAvailable, "FreeRDP kütüphanesi yok");
        using var session = new RdpSession();
        var ended = new TaskCompletionSource<RdpEndReason>();
        session.Ended += (reason, _) => ended.TrySetResult(reason);

        session.Start(To("127.0.0.1", 1));

        Assert.Equal(RdpEndReason.ConnectFailed, await ended.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void DisposeWhileConnectingDoesNotCrash()
    {
        Assert.SkipUnless(RdpSession.IsAvailable, "FreeRDP kütüphanesi yok");
        var session = new RdpSession();
        session.Start(To("10.255.255.1", 3389));
        Thread.Sleep(200);

        var watch = Stopwatch.StartNew();
        session.Dispose();

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(6), $"{watch.Elapsed}");
    }

    [Fact]
    public void DisposeTwiceIsSafe()
    {
        Assert.SkipUnless(RdpSession.IsAvailable, "FreeRDP kütüphanesi yok");
        var session = new RdpSession();
        session.Start(To("127.0.0.1", 1));
        session.Dispose();
        session.Dispose();
    }

    [Fact]
    public void DisposeWithoutStartIsSafe() => new RdpSession().Dispose();
}

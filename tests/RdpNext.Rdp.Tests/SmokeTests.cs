namespace RdpNext.Rdp.Tests;

public class SmokeTests
{
    [Fact]
    public async Task ConnectsAndReceivesFrames()
    {
        var options = SmokeServer.Options();
        Assert.SkipWhen(options is null, "RDPNEXT_SMOKE ayarlı değil");
        using var session = new RdpSession();
        var connected = new TaskCompletionSource();
        var frame = new TaskCompletionSource<(int, int)>();
        session.Connected += () => connected.TrySetResult();
        session.Frame += f => frame.TrySetResult((f.Width, f.Height));
        session.Ended += (r, _) => connected.TrySetException(new Exception(r.ToString()));

        session.Start(options!);

        var timeout = TimeSpan.FromSeconds(20);
        await connected.Task.WaitAsync(timeout, TestContext.Current.CancellationToken);
        Assert.Equal((1280, 800), await frame.Task.WaitAsync(timeout, TestContext.Current.CancellationToken));
    }

    // Sunucu dinamik çözünürlüğü destekliyorsa yeni boyutta kare gelir.
    [Fact]
    public async Task ResizeChangesRemoteResolution()
    {
        var options = SmokeServer.Options();
        Assert.SkipWhen(options is null, "RDPNEXT_SMOKE ayarlı değil");
        using var session = new RdpSession();
        var connected = new TaskCompletionSource();
        var resized = new TaskCompletionSource<(int, int)>();
        session.Connected += () => connected.TrySetResult();
        session.Frame += f =>
        {
            if (f.Width != 1280)
                resized.TrySetResult((f.Width, f.Height));
        };

        session.Start(options!);
        await connected.Task.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        await Task.Delay(3000, TestContext.Current.CancellationToken);
        session.Resize(1000, 700);

        Assert.Equal((1000, 700), await resized.Task.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken));
    }
}

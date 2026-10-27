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
}

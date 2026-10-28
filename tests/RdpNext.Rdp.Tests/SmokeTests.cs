namespace RdpNext.Rdp.Tests;

public class SmokeTests
{
    [Fact]
    public async Task ConnectsAndReceivesFrames()
    {
        var options = SmokeServer.Options();
        Assert.SkipWhen(options is null, "RDPNEXT_SMOKE ayarlı değil");
        using var session = new RdpSession();
        RdpCertificate? asked = null;
        session.VerifyCertificate = c =>
        {
            asked = c;
            return Task.FromResult(true);
        };
        var connected = new TaskCompletionSource();
        var frame = new TaskCompletionSource<(int, int)>();
        session.Connected += () => connected.TrySetResult();
        session.Frame += f => frame.TrySetResult((f.Width, f.Height));
        session.Ended += (r, _) => connected.TrySetException(new Exception(r.ToString()));

        session.Start(options!);

        var timeout = TimeSpan.FromSeconds(20);
        await connected.Task.WaitAsync(timeout, TestContext.Current.CancellationToken);
        Assert.Equal((1280, 800), await frame.Task.WaitAsync(timeout, TestContext.Current.CancellationToken));
        // arsaVev'in sertifikası IP ile eşleşmiyor; soru gelmiş olmalı.
        Assert.False(string.IsNullOrEmpty(asked?.Fingerprint));
    }

    // Sunucu dinamik çözünürlüğü destekliyorsa yeni boyutta kare gelir.
    [Fact]
    public async Task ResizeChangesRemoteResolution()
    {
        var options = SmokeServer.Options();
        Assert.SkipWhen(options is null, "RDPNEXT_SMOKE ayarlı değil");
        using var session = new RdpSession { VerifyCertificate = _ => Task.FromResult(true) };
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

    [Fact]
    public async Task RejectedCertificateEndsWithCertificateRejected()
    {
        var options = SmokeServer.Options();
        Assert.SkipWhen(options is null, "RDPNEXT_SMOKE ayarlı değil");
        using var session = new RdpSession { VerifyCertificate = _ => Task.FromResult(false) };
        var ended = new TaskCompletionSource<RdpEndReason>();
        session.Ended += (r, _) => ended.TrySetResult(r);

        session.Start(options!);

        Assert.Equal(RdpEndReason.CertificateRejected, await ended.Task.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken));
    }

    // Hesabı kilitlememek için yalnız RDPNEXT_SMOKE_WRONG_PASSWORD=1 ile, bilerek çalıştırılır.
    [Fact]
    public async Task WrongPasswordEndsWithAuthFailed()
    {
        var options = SmokeServer.Options();
        Assert.SkipWhen(options is null || Environment.GetEnvironmentVariable("RDPNEXT_SMOKE_WRONG_PASSWORD") != "1",
            "RDPNEXT_SMOKE_WRONG_PASSWORD ayarlı değil");
        using var session = new RdpSession { VerifyCertificate = _ => Task.FromResult(true) };
        var ended = new TaskCompletionSource<RdpEndReason>();
        session.Ended += (r, _) => ended.TrySetResult(r);

        session.Start(options! with { Password = "rdpNext-wrong-" + Guid.NewGuid() });

        Assert.Equal(RdpEndReason.AuthFailed, await ended.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
    }

    // Pano iki yönde: Mac'teki metin sunucuda Çalıştır kutusuna yapıştırılır, oradan kopyalanıp geri gelir.
    // Sunucuda yalnız Win+R kutusu açılıp Esc ile kapanır; hiçbir şey çalıştırılmaz.
    [Fact]
    public async Task ClipboardRoundTrip()
    {
        var options = SmokeServer.Options();
        Assert.SkipWhen(options is null, "RDPNEXT_SMOKE ayarlı değil");
        using var session = new RdpSession { VerifyCertificate = _ => Task.FromResult(true) };
        var connected = new TaskCompletionSource();
        var back = new TaskCompletionSource<string>();
        var token = "rdpNext-pano-" + Guid.NewGuid().ToString("N")[..8];
        session.Connected += () => connected.TrySetResult();
        session.RemoteClipboardText += text =>
        {
            if (text?.Contains(token) == true)
                back.TrySetResult(text!);
        };
        var ct = TestContext.Current.CancellationToken;
        void Tap(ushort code, bool extended = false)
        {
            session.SendKey(code, extended, true);
            session.SendKey(code, extended, false);
        }
        void Ctrl(ushort code)
        {
            session.SendKey(0x1D, false, true);
            Tap(code);
            session.SendKey(0x1D, false, false);
        }

        session.Start(options!);
        await connected.Task.WaitAsync(TimeSpan.FromSeconds(20), ct);
        await Task.Delay(3000, ct);
        session.SetLocalClipboard(token, []);
        await Task.Delay(500, ct);
        session.SendKey(0x5B, true, true);
        Tap(0x13);
        session.SendKey(0x5B, true, false);
        await Task.Delay(1500, ct);
        Ctrl(0x2F);
        await Task.Delay(800, ct);
        Ctrl(0x1E);
        Ctrl(0x2E);
        try
        {
            Assert.Contains(token, await back.Task.WaitAsync(TimeSpan.FromSeconds(10), ct));
        }
        finally
        {
            Tap(0x01);
            await Task.Delay(500, ct);
        }
    }
}

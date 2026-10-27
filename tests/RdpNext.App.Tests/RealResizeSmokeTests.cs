using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using RdpNext.App.ViewModels;
using RdpNext.Core.Credentials;
using RdpNext.Core.Secrets;
using RdpNext.Rdp;

namespace RdpNext.App.Tests;

// Gerçek sunucuyla, uygulamanın oturum sekmesi üzerinden: bağlandıktan sonra görünüm büyüyünce sunucu da büyümeli.
// Yalnız RDPNEXT_SMOKE="host:port:kullanıcı:düğüm Id" ayarlıyken koşar.
public class RealResizeSmokeTests
{
    [AvaloniaFact]
    public void ViewResizeReachesRealServer() => ResizeAfterConnect(waitBeforeResize: true);

    // Tam ekran açılışı: bağlanır bağlanmaz yeni boyut istenir, sunucunun çözünürlük kanalı henüz hazır değildir.
    [AvaloniaFact]
    public void ResizeRightAfterConnectReachesRealServer() => ResizeAfterConnect(waitBeforeResize: false);

    private static void ResizeAfterConnect(bool waitBeforeResize)
    {
        var value = Environment.GetEnvironmentVariable("RDPNEXT_SMOKE");
        if (string.IsNullOrEmpty(value) || !OperatingSystem.IsMacOS())
        {
            Assert.Skip("RDPNEXT_SMOKE ayarlı değil");
            return;
        }
        var parts = value!.Split(':');
        var password = new MacKeychainSecretStore().GetPassword(Guid.Parse(parts[3]))!;
        var server = new Server { Name = "smoke", Host = parts[0], Port = int.Parse(parts[1]), CertificateFingerprint = null };
        var tab = new SessionTabViewModel(new ServerNodeViewModel(server));
        var sizes = new List<(int, int)>();
        tab.Connect(() =>
        {
            var session = new RdpSession { VerifyCertificate = _ => Task.FromResult(true) };
            session.Frame += f => { lock (sizes) sizes.Add((f.Width, f.Height)); };
            return session;
        }, new ResolvedCredentials(parts[2], password, null), new Avalonia.Size(1374, 915));
        var session = tab.Session!;
        // Sertifika sorusu arayüz iş parçacığına gider; testte kabul edilir.
        Wait(() => tab.CertificatePrompt is not null || tab.State == SessionState.Connected, 20);
        if (tab.CertificatePrompt is not null)
            tab.TrustCertificateCommand.Execute(null);
        Wait(() => tab.State == SessionState.Connected, 20);
        if (waitBeforeResize)
        {
            Wait(() => { lock (sizes) return sizes.Count > 0; }, 10);
            Thread.Sleep(3000);
            Dispatcher.UIThread.RunJobs();
        }

        tab.ViewSizeChanged(1710, 1112);

        Wait(() => { lock (sizes) return sizes.Contains((1710, 1112)); }, 15);
        tab.OnClosed();
        lock (sizes)
            Assert.Contains((1710, 1112), sizes);
    }

    private static void Wait(Func<bool> done, int seconds)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        while (!done() && DateTime.UtcNow < until)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(50);
        }
    }
}

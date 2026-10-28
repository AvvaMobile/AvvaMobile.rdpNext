using System.Text;

namespace RdpNext.Ssh.Tests;

// Docker'daki test sunucusuna gerçek bağlantı. Aynı kapsayıcıyı kullandıkları için sırayla koşarlar.
public sealed class SshSessionTests(SshServerFixture server) : IClassFixture<SshServerFixture>
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    private sealed class Run : IDisposable
    {
        private readonly StringBuilder _text = new();
        private readonly Decoder _decoder = Encoding.UTF8.GetDecoder();

        public Run(SshConnectOptions options, Func<SshHostKey, Task<bool>>? verify = null)
        {
            Session.VerifyHostKey = verify ?? (_ => Task.FromResult(true));
            Session.Connected += () => ConnectedTask.TrySetResult();
            Session.Output += bytes =>
            {
                var chars = new char[_decoder.GetCharCount(bytes, 0, bytes.Length)];
                _decoder.GetChars(bytes, 0, bytes.Length, chars, 0);
                lock (_text)
                    _text.Append(chars);
            };
            Session.Ended += (reason, _) => EndedTask.TrySetResult(reason);
            Session.Start(options);
        }

        public SshSession Session { get; } = new();

        public TaskCompletionSource ConnectedTask { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<SshEndReason> EndedTask { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Text
        {
            get
            {
                lock (_text)
                    return _text.ToString();
            }
        }

        public async Task Connected()
        {
            var first = await Task.WhenAny(ConnectedTask.Task, EndedTask.Task).WaitAsync(Timeout, TestContext.Current.CancellationToken);
            Assert.True(first == ConnectedTask.Task, $"bağlanmadı: {(EndedTask.Task.IsCompleted ? EndedTask.Task.Result : "?")}");
        }

        public Task<SshEndReason> Ended() => EndedTask.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        public void Type(string text) => Session.Send(Encoding.UTF8.GetBytes(text));

        public async Task WaitFor(string text)
        {
            var until = DateTime.UtcNow + Timeout;
            while (!Text.Contains(text))
            {
                Assert.True(DateTime.UtcNow < until, $"'{text}' gelmedi. Çıktı:\n{Text}");
                await Task.Delay(50, TestContext.Current.CancellationToken);
            }
        }

        public void Dispose() => Session.Dispose();
    }

    private SshConnectOptions Options(string? password = SshServerFixture.Password, string? key = null, string? passphrase = null,
        string user = SshServerFixture.User, int port = SshServerFixture.Port) =>
        new(SshServerFixture.Host, port, user, password, key is null ? null : server.Key(key), passphrase, 80, 24);

    private void SkipWithoutDocker() => Assert.SkipUnless(server.Available, "Docker test sunucusu yok");

    [Fact]
    public async Task ConnectsWithPasswordAndRunsCommand()
    {
        SkipWithoutDocker();
        using var run = new Run(Options());
        await run.Connected();

        run.Type("echo mer''haba\n");

        await run.WaitFor("merhaba");
    }

    [Theory]
    [InlineData("id_ed25519")]
    [InlineData("id_rsa")]
    [InlineData("id_ecdsa")]
    public async Task ConnectsWithEachKeyType(string key)
    {
        SkipWithoutDocker();
        using var run = new Run(Options(password: null, key: key));
        await run.Connected();

        run.Type("echo an''ahtar\n");

        await run.WaitFor("anahtar");
    }

    [Fact]
    public async Task EncryptedKeyWithPassphraseConnects()
    {
        SkipWithoutDocker();
        using var run = new Run(Options(password: null, key: "id_ed25519_pw", passphrase: SshServerFixture.Passphrase));
        await run.Connected();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("yanlis")]
    public async Task EncryptedKeyWithoutRightPassphraseEndsWithPassphraseNeeded(string? passphrase)
    {
        SkipWithoutDocker();
        using var run = new Run(Options(password: null, key: "id_ed25519_pw", passphrase: passphrase));

        Assert.Equal(SshEndReason.PassphraseNeeded, await run.Ended());
    }

    [Fact]
    public async Task MissingKeyFileEndsWithKeyUnusable()
    {
        SkipWithoutDocker();
        using var run = new Run(Options(password: null, key: "yok_boyle_anahtar"));

        Assert.Equal(SshEndReason.KeyUnusable, await run.Ended());
    }

    [Fact]
    public async Task EmptyKeyFileEndsWithKeyUnusable()
    {
        SkipWithoutDocker();
        using var run = new Run(Options(password: SshServerFixture.Password) with { KeyFile = "" });

        Assert.Equal(SshEndReason.KeyUnusable, await run.Ended());
    }

    // Sekme bağlanırken kapanırsa sunucuda açık oturum kalmaz.
    [Fact]
    public async Task DisposeWhileConnectingLeavesNoSessionOnServer()
    {
        SkipWithoutDocker();
        for (var i = 0; i < 5; i++)
        {
            var run = new Run(Options());
            await Task.Delay(40 * i, TestContext.Current.CancellationToken);
            run.Dispose();
        }
        await Task.Delay(3000, TestContext.Current.CancellationToken);

        var ps = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("docker", "exec rdpnext-ssh-test ps -o etime,args")
            { RedirectStandardOutput = true })!;
        var processes = await ps.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("sshd: deneme", processes);
    }

    // Sunucunun tanımadığı anahtar: şifreye düşülmez.
    [Fact]
    public async Task RejectedKeyEndsWithKeyUnusable()
    {
        SkipWithoutDocker();
        var stranger = Path.Combine(Path.GetTempPath(), "rdpnext-stranger-" + Guid.NewGuid());
        Assert.Equal(0, SshServerFixture.Run("ssh-keygen", $"-q -t ed25519 -N \"\" -f {stranger}"));
        try
        {
            using var run = new Run(Options(password: SshServerFixture.Password, key: null) with { KeyFile = stranger, Password = null });

            Assert.Equal(SshEndReason.KeyUnusable, await run.Ended());
        }
        finally
        {
            File.Delete(stranger);
            File.Delete(stranger + ".pub");
        }
    }

    [Fact]
    public async Task WrongPasswordEndsWithAuthFailed()
    {
        SkipWithoutDocker();
        using var run = new Run(Options(password: "yanlis"));

        Assert.Equal(SshEndReason.AuthFailed, await run.Ended());
    }

    [Fact]
    public async Task ClosedPortEndsWithConnectFailed()
    {
        SkipWithoutDocker();
        using var run = new Run(Options(port: 1));

        Assert.Equal(SshEndReason.ConnectFailed, await run.Ended());
    }

    [Fact]
    public async Task HostKeyAskedWithSha256Fingerprint()
    {
        SkipWithoutDocker();
        SshHostKey? asked = null;
        using var run = new Run(Options(), key =>
        {
            asked = key;
            return Task.FromResult(true);
        });
        await run.Connected();

        Assert.Equal("ssh-ed25519", asked!.Algorithm);
        Assert.Matches("^SHA256:[A-Za-z0-9+/]{43}$", asked.Fingerprint);
    }

    [Fact]
    public async Task RejectedHostKeyEndsWithHostKeyRejected()
    {
        SkipWithoutDocker();
        using var run = new Run(Options(), _ => Task.FromResult(false));

        Assert.Equal(SshEndReason.HostKeyRejected, await run.Ended());
        Assert.False(run.ConnectedTask.Task.IsCompleted);
    }

    [Fact]
    public async Task ExitEndsWithExited()
    {
        SkipWithoutDocker();
        using var run = new Run(Options());
        await run.Connected();

        run.Type("exit\n");

        Assert.Equal(SshEndReason.Exited, await run.Ended());
    }

    // Sunucu okumazken büyük yapıştırma: Send çağıranı (arayüzü) bekletmez, veri sırayla ve eksiksiz gider.
    [Fact]
    public async Task LargePasteDoesNotBlockCaller()
    {
        SkipWithoutDocker();
        using var run = new Run(Options());
        await run.Connected();
        run.Type("stty -echo; echo HA''ZIR\n");
        await run.WaitFor("HAZIR");
        run.Type("sleep 3; head -c 6000000 | tail -c 5; echo; echo BI''TTI\n");
        var line = new string('a', 999) + "\n";
        var paste = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat(line, 5999)) + new string('a', 996) + "SON\n");

        var watch = System.Diagnostics.Stopwatch.StartNew();
        run.Session.Send(paste);
        watch.Stop();

        Assert.True(watch.ElapsedMilliseconds < 500, $"Send {watch.ElapsedMilliseconds} ms bekletti");
        await run.WaitFor("aSON");
        await run.WaitFor("BITTI");
    }

    [Fact]
    public async Task TurkishCharactersRoundTrip()
    {
        SkipWithoutDocker();
        using var run = new Run(Options());
        await run.Connected();

        run.Type("echo \"<ğüşıöçİ>\" | tr -d x\n");

        await run.WaitFor("<ğüşıöçİ>\r\n");
    }

    [Fact]
    public async Task ResizeChangesStty()
    {
        SkipWithoutDocker();
        using var run = new Run(Options());
        await run.Connected();

        run.Session.Resize(100, 30);
        run.Type("stty size | sed 's/^/boyut=/'\n");

        await run.WaitFor("boyut=30 100");
    }

    // Kapsayıcı yeniden başlarsa bağlantı kopar; kabuktan çıkılmış sayılmaz.
    [Fact]
    public async Task ServerRestartEndsWithDisconnected()
    {
        SkipWithoutDocker();
        using var run = new Run(Options());
        await run.Connected();

        Assert.Equal(0, SshServerFixture.Run("docker", "restart -t 0 rdpnext-ssh-test"));

        Assert.Equal(SshEndReason.Disconnected, await run.Ended());
        await WaitForServer();
    }

    private static async Task WaitForServer()
    {
        for (var i = 0; i < 50; i++)
        {
            try
            {
                using var tcp = new System.Net.Sockets.TcpClient();
                await tcp.ConnectAsync(SshServerFixture.Host, SshServerFixture.Port, TestContext.Current.CancellationToken);
                await Task.Delay(300, TestContext.Current.CancellationToken);
                return;
            }
            catch (System.Net.Sockets.SocketException)
            {
                await Task.Delay(200, TestContext.Current.CancellationToken);
            }
        }
    }
}

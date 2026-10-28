using System.Diagnostics;

namespace RdpNext.Ssh.Tests;

// Docker'da yerel OpenSSH sunucusu (server/start.sh). Docker yoksa testler atlanır.
public sealed class SshServerFixture
{
    public const string Host = "127.0.0.1";
    public const int Port = 2222;
    public const string User = "deneme";
    public const string Password = "deneme123";
    public const string Passphrase = "anahtar123";

    public SshServerFixture()
    {
        ServerDirectory = FindServerDirectory();
        Available = ServerDirectory is not null && Run("docker", "info") == 0
                    && Run(Path.Combine(ServerDirectory, "start.sh"), Port.ToString()) == 0;
    }

    public bool Available { get; }

    public string? ServerDirectory { get; }

    public string Key(string name) => Path.Combine(ServerDirectory!, "keys", name);

    public static int Run(string file, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(file, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            })!;
            process.WaitForExit(TimeSpan.FromMinutes(3));
            return process.HasExited ? process.ExitCode : -1;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return -1;
        }
    }

    private static string? FindServerDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "tests", "RdpNext.Ssh.Tests", "server");
            if (File.Exists(Path.Combine(candidate, "start.sh")))
                return candidate;
        }
        return null;
    }
}

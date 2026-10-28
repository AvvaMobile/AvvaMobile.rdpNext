using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using RdpNext.App;
using RdpNext.App.ViewModels;
using RdpNext.App.Views;
using RdpNext.Core.Model;
using RdpNext.Core.Secrets;
using RdpNext.Core.Storage;
using RdpNext.Rdp;

namespace RdpNext.Screenshots;

// Site ve README görsellerini demo veriyle, ekrana dokunmadan çizer.
// Kullanım: dotnet run --project tools/Screenshots -- site/images
public static class Program
{
    private const double Scale = 2;

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App.App>().UseSkia().WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    public static void Main(string[] args)
    {
        var output = Path.GetFullPath(args.Length > 0 ? args[0] : "site/images");
        Directory.CreateDirectory(output);
        using var session = HeadlessUnitTestSession.StartNew(typeof(Program));
        session.Dispatch(() =>
        {
            foreach (var theme in new[] { AppTheme.Dark, AppTheme.Light })
            {
                var suffix = theme == AppTheme.Dark ? "dark" : "light";
                Shoot(Path.Combine(output, $"main-{suffix}.png"), theme, MainScreen);
                Shoot(Path.Combine(output, $"ssh-{suffix}.png"), theme, SshTerminal);
                Shoot(Path.Combine(output, $"editor-{suffix}.png"), theme, Editor);
                Shoot(Path.Combine(output, $"import-{suffix}.png"), theme, Import);
                Shoot(Path.Combine(output, $"search-{suffix}.png"), theme, Search);
                Shoot(Path.Combine(output, $"certificate-{suffix}.png"), theme, Certificate);
            }
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    private sealed class Demo(MainWindowViewModel vm, MainWindow window, Group root, List<DemoRdpSession> rdp, List<DemoSshSession> ssh)
    {
        public MainWindowViewModel Vm { get; } = vm;

        public MainWindow Window { get; } = window;

        public List<DemoRdpSession> Rdp { get; } = rdp;

        public List<DemoSshSession> Ssh { get; } = ssh;

        public ServerNodeViewModel Server(string name) =>
            Vm.Tree.Node<ServerNodeViewModel>(root.AllServers().First(s => s.Name == name).Id);

        // Bağlanır; RDP ise masaüstü, SSH ise verilen çıktı görünür.
        public void Connect(string name, string? terminal = null)
        {
            Vm.ConnectCommand.Execute(Server(name));
            Settle();
            if (Vm.Sessions.Selected is SshTabViewModel)
            {
                Ssh[^1].RaiseConnected();
                Settle();
                Ssh[^1].RaiseOutput(Encoding.UTF8.GetBytes(terminal ?? Terminal(name)));
            }
            else
            {
                Rdp[^1].RaiseConnected();
                Settle();
                var view = Window.GetVisualDescendants().OfType<SessionView>().First(v => v.IsEffectivelyVisible);
                var (pixels, width, height) = FakeDesktop.Render(view.Bounds.Width, view.Bounds.Height, Scale);
                Rdp[^1].RaiseFrame(pixels, width, height);
            }
            Settle();
        }

        public void Select(TabViewModel tab)
        {
            Vm.Sessions.Selected = tab;
            Settle();
        }
    }

    private static void Shoot(string file, AppTheme theme, Action<Demo> scene)
    {
        Themes.Apply(theme);
        var dir = Directory.CreateTempSubdirectory("rdpnext-shots-").FullName;
        var path = Path.Combine(dir, "connections.json");
        var secrets = new InMemorySecretStore();
        var root = DemoData.Build(secrets);
        new ConnectionStore(path).Save(root);
        var rdp = new List<DemoRdpSession>();
        var ssh = new List<DemoSshSession>();
        var vm = new MainWindowViewModel(new ConnectionStore(path), secrets,
            rdp: () => { var s = new DemoRdpSession(); rdp.Add(s); return s; },
            ssh: () => { var s = new DemoSshSession(); ssh.Add(s); return s; });
        // 2x DPI ile çizimde gölgeli kutuların içi kayıyor; içerik ölçek dönüşümüyle büyütülür, 96 DPI çizilir.
        var window = new MainWindow { DataContext = vm, Width = 1280 * Scale, Height = 860 * Scale };
        var content = (Control)window.Content!;
        window.Content = null;
        window.Content = new LayoutTransformControl { LayoutTransform = new ScaleTransform(Scale, Scale), Child = content };
        window.Show();
        Settle();
        var loaded = vm.Store.Load().Root;
        var demo = new Demo(vm, window, loaded, rdp, ssh);
        scene(demo);
        // Açılış animasyonları bitsin.
        for (var i = 0; i < 60; i++)
            Settle();

        using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height));
        bitmap.Render(window);
        bitmap.Save(file, PngBitmapEncoderOptions.Default);
        vm.Shutdown();
        window.Close();
        Settle();
        Directory.Delete(dir, true);
        Console.WriteLine(file);
    }

    private static void Settle()
    {
        for (var i = 0; i < 5; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private static void MainScreen(Demo demo)
    {
        demo.Connect("sql-01");
        demo.Connect("ubuntu-build");
        demo.Connect("web-01");
        demo.Vm.Tree.Selected = demo.Server("web-01");
    }

    private static void SshTerminal(Demo demo)
    {
        demo.Connect("web-01");
        demo.Connect("k8s-node-01");
        demo.Connect("ubuntu-build");
        demo.Vm.Tree.Selected = demo.Server("ubuntu-build");
    }

    private static void Editor(Demo demo)
    {
        demo.Connect("web-01");
        demo.Vm.Tree.Selected = demo.Server("sql-01");
        demo.Vm.EditServer(demo.Server("sql-01"));
    }

    private static void Import(Demo demo)
    {
        demo.Connect("web-01");
        demo.Vm.OpenSettingsCommand.Execute(null);
        Settle();
        var settings = (SettingsViewModel)demo.Vm.Sessions.Selected!;
        settings.ShowImport = true;
    }

    private static void Search(Demo demo)
    {
        demo.Connect("web-01");
        demo.Vm.Tree.Query = "sql";
    }

    private static void Certificate(Demo demo)
    {
        demo.Vm.ConnectCommand.Execute(demo.Server("acme-dc01"));
        Settle();
        _ = demo.Rdp[^1].VerifyCertificate!(new RdpCertificate(
            "CN=acme-dc01.acme.example.com", "CN=acme-dc01.acme.example.com",
            "3B:9F:21:C4:7A:0E:58:D2:91:6C:44:AF:12:8B:E7:30:5D:C9:66:0A", false));
    }

    private static string Terminal(string name)
    {
        const string green = "\e[1;32m", blue = "\e[1;34m", dim = "\e[2m", reset = "\e[0m", cyan = "\e[36m", yellow = "\e[33m";
        var prompt = $"{green}deploy@{name}{reset}:{blue}~{reset}$ ";
        return
            $"Welcome to Ubuntu 24.04.1 LTS (GNU/Linux 6.8.0-45-generic x86_64)\r\n\r\n" +
            $"  System load:  0.08               Processes:             142\r\n" +
            $"  Usage of /:   23.4% of 96.73GB   Users logged in:       1\r\n" +
            $"  Memory usage: 18%                IPv4 address for eth0: 10.0.3.10\r\n\r\n" +
            $"{dim}Last login: Tue Oct 27 09:14:02 2026 from 10.0.0.5{reset}\r\n" +
            $"{prompt}docker ps --format \"table {{{{.Names}}}}\\t{{{{.Image}}}}\\t{{{{.Status}}}}\"\r\n" +
            $"NAMES       IMAGE                  STATUS\r\n" +
            $"edge        nginx:1.27             {green}Up 3 days{reset}\r\n" +
            $"api         ghcr.io/acme/api:4.2   {green}Up 3 days{reset}\r\n" +
            $"worker      ghcr.io/acme/jobs:4.2  {green}Up 3 days{reset}\r\n" +
            $"postgres    postgres:17            {green}Up 12 days (healthy){reset}\r\n" +
            $"redis       redis:7.4              {green}Up 12 days{reset}\r\n" +
            $"{prompt}ls\r\n" +
            $"{blue}apps{reset}  {blue}backups{reset}  {cyan}current{reset}  deploy.sh  {yellow}docker-compose.yml{reset}  {blue}logs{reset}\r\n" +
            $"{prompt}";
    }
}

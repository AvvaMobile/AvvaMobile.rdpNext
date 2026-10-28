using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using RdpNext.App.Services;
using RdpNext.App.ViewModels;
using RdpNext.Core.Secrets;
using RdpNext.Core.Storage;

namespace RdpNext.App.Tests;

public class SettingsTests
{
    // sure: "emin misin" sorusunun cevabı.
    private sealed class FakeConfirm(bool sure) : IConfirmService
    {
        public List<string> Asked { get; } = [];

        public Task<bool> ConfirmAsync(string message, string confirmText)
        {
            Asked.Add(message);
            return Task.FromResult(sure);
        }
    }

    private sealed class FakeFiles(string? path) : IFileDialogService
    {
        public string? SuggestedName { get; private set; }

        public Task<string?> SaveAsync(string suggestedName)
        {
            SuggestedName = suggestedName;
            return Task.FromResult(path);
        }

        public Task<string?> OpenAsync() => Task.FromResult(path);

        public Task<string?> OpenKeyFileAsync() => Task.FromResult<string?>(null);

        public Task<string?> OpenMRemoteNgAsync() => Task.FromResult(path);
    }

    private readonly string _path = TestWindows.SampleFile();
    private readonly string _backup = Path.Combine(Path.GetTempPath(), "rdpnext-backup-" + Guid.NewGuid() + ".json");

    private FakeConfirm _confirm = new(true);

    private MainWindowViewModel Create(string? picked, bool sure = true)
    {
        _confirm = new FakeConfirm(sure);
        return new(new ConnectionStore(_path), _secrets, _confirm, new FakeFiles(picked));
    }

    private readonly InMemorySecretStore _secrets = new();

    private void SaveBackup()
    {
        var backup = new Group();
        var folder = new Group { Name = "Yedek klasör" };
        folder.Add(new Server { Name = "yedek-01", Host = "10.9.9.9" });
        backup.Add(folder);
        new ConnectionStore(_backup).Save(backup);
    }

    private static SettingsViewModel OpenSettings(MainWindowViewModel vm)
    {
        vm.OpenSettingsCommand.Execute(null);
        return Assert.IsType<SettingsViewModel>(vm.Sessions.Selected);
    }

    [Fact]
    public async Task ExportWritesWholeList()
    {
        var vm = Create(_backup);

        await OpenSettings(vm).ExportCommand.ExecuteAsync(null);

        var root = ConnectionStore.Read(_backup)!;
        Assert.Equal(["Üretim", "Test", "Müşteriler"], root.Children.Select(n => n.Name));
        Assert.Equal(vm.Tree.Root.AllServers().Count(), root.AllServers().Count());
    }

    [Fact]
    public async Task ExportSuggestsDatedName()
    {
        var vm = Create(null);

        await OpenSettings(vm).ExportCommand.ExecuteAsync(null);

        Assert.Equal($"rdpNext-{DateTime.Now:yyyy-MM-dd}.json", ((FakeFiles)vm.Files).SuggestedName);
    }

    [Fact]
    public async Task ExportCancelledWritesNothing()
    {
        var vm = Create(null);

        await OpenSettings(vm).ExportCommand.ExecuteAsync(null);

        Assert.False(File.Exists(_backup));
    }

    [Fact]
    public async Task ImportAddsToListByDefault()
    {
        SaveBackup();
        var vm = Create(_backup);
        var tab = vm.Sessions.Open(vm.Tree.Node<ServerNodeViewModel>(vm.Tree.Root.AllServers().First().Id));
        var settings = OpenSettings(vm);

        await settings.ImportCommand.ExecuteAsync(null);

        Assert.Equal(["Üretim", "Test", "Müşteriler", "Yedek klasör"], vm.Tree.Roots.Select(n => n.Name));
        Assert.Contains(tab, vm.Sessions.Tabs);
        Assert.Empty(_confirm.Asked);
        Assert.Equal(4, new ConnectionStore(_path).Load().Root.Children.Count);
    }

    // Aynı yedeği iki kez eklemek kayıtları çoğaltmaz; klasörde yeni olan sunucu klasöre eklenir.
    [Fact]
    public async Task ImportSkipsRecordsAlreadyInList()
    {
        var vm = Create(_backup);
        new ConnectionStore(_backup).Save(new ConnectionStore(_path).Load().Root);
        var copy = ConnectionStore.Read(_backup)!;
        ((Group)copy.Children[0]).Add(new Server { Name = "yeni-01" });
        new ConnectionStore(_backup).Save(copy);
        var before = vm.Tree.Root.AllServers().Count();

        await OpenSettings(vm).ImportCommand.ExecuteAsync(null);

        Assert.Equal(3, vm.Tree.Roots.Count);
        Assert.Equal(before + 1, vm.Tree.Root.AllServers().Count());
        Assert.Equal("yeni-01", ((GroupNodeViewModel)vm.Tree.Roots[0]).Children.Last().Name);
    }

    [Fact]
    public async Task ImportWithDeleteAsksOnceThenReplaces()
    {
        SaveBackup();
        var vm = Create(_backup, sure: true);
        var old = vm.Tree.Root.AllServers().First();
        vm.Secrets.SetPassword(old.Id, "eski");
        vm.Sessions.Open(vm.Tree.Node<ServerNodeViewModel>(old.Id));
        var settings = OpenSettings(vm);
        settings.DeleteOld = true;

        await settings.ImportCommand.ExecuteAsync(null);

        Assert.Single(_confirm.Asked);
        Assert.Equal(["Yedek klasör"], vm.Tree.Roots.Select(n => n.Name));
        Assert.Equal([settings], vm.Sessions.Tabs);
        Assert.Null(vm.Secrets.GetPassword(old.Id));
        Assert.Equal(["Yedek klasör"], new ConnectionStore(_path).Load().Root.Children.Select(n => n.Name));
    }

    [Fact]
    public async Task ImportWithDeleteNotSureKeepsList()
    {
        SaveBackup();
        var vm = Create(_backup, sure: false);
        var settings = OpenSettings(vm);
        settings.DeleteOld = true;

        await settings.ImportCommand.ExecuteAsync(null);

        Assert.Equal(3, vm.Tree.Roots.Count);
    }

    [Fact]
    public async Task ImportBadFileKeepsListAndFile()
    {
        File.WriteAllText(_backup, "{ yarım");
        var vm = Create(_backup);
        var settings = OpenSettings(vm);

        await settings.ImportCommand.ExecuteAsync(null);

        Assert.Equal(3, vm.Tree.Roots.Count);
        Assert.Equal("{ yarım", File.ReadAllText(_backup));
        Assert.NotNull(settings.Status);
    }

    private static string MRemoteNg(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "mRemoteNG", name);

    private static readonly Guid Web02 = new("6f1c2a10-0000-4000-8000-000000000012");

    [Fact]
    public async Task MRemoteNgImportAddsFolderWithPasswords()
    {
        var vm = Create(MRemoteNg("confCons.xml"));
        var settings = OpenSettings(vm);

        await settings.ImportMRemoteNgCommand.ExecuteAsync(null);

        Assert.Equal(["Üretim", "Test", "Müşteriler", "mRemoteNG"], vm.Tree.Roots.Select(n => n.Name));
        Assert.Equal("ali-ğüş", vm.Secrets.GetPassword(Web02));
        Assert.Equal("Imported, 4 new servers added. 2 connections skipped (only RDP and SSH are supported).", settings.MRemoteNgStatus);
        Assert.Null(settings.Status);
        Assert.Equal("mRemoteNG", new ConnectionStore(_path).Load().Root.Children.Last().Name);
    }

    // Aynı dosya tekrar alınınca kayıt çoğalmaz, sonradan değiştirilen şifre ezilmez.
    [Fact]
    public async Task MRemoteNgImportTwiceKeepsChangedPassword()
    {
        var vm = Create(MRemoteNg("confCons.xml"));
        var settings = OpenSettings(vm);
        await settings.ImportMRemoteNgCommand.ExecuteAsync(null);
        var count = vm.Tree.Root.AllServers().Count();
        vm.Secrets.SetPassword(Web02, "degisti");

        await settings.ImportMRemoteNgCommand.ExecuteAsync(null);

        Assert.Equal(count, vm.Tree.Root.AllServers().Count());
        Assert.Equal("degisti", vm.Secrets.GetPassword(Web02));
        Assert.StartsWith("Imported, 0 new servers added.", settings.MRemoteNgStatus);
    }

    [Fact]
    public async Task MRemoteNgMasterPasswordAskedThenChecked()
    {
        var vm = Create(MRemoteNg("confCons-master.xml"));
        var settings = OpenSettings(vm);

        await settings.ImportMRemoteNgCommand.ExecuteAsync(null);
        Assert.Equal("This file is protected with a master password. Enter it above and import again.", settings.MRemoteNgStatus);

        settings.MasterPassword = "yanlis";
        await settings.ImportMRemoteNgCommand.ExecuteAsync(null);
        Assert.Equal("Wrong master password.", settings.MRemoteNgStatus);
        Assert.Equal(3, vm.Tree.Roots.Count);

        settings.MasterPassword = "ana-parola";
        await settings.ImportMRemoteNgCommand.ExecuteAsync(null);
        Assert.Equal("ali-ğüş", vm.Secrets.GetPassword(Web02));
    }

    [Fact]
    public async Task MRemoteNgOtherFileShowsCouldNotRead()
    {
        var vm = Create(MRemoteNg("not-mremoteng.xml"));
        var settings = OpenSettings(vm);

        await settings.ImportMRemoteNgCommand.ExecuteAsync(null);

        Assert.Equal("Could not read the file: not-mremoteng.xml", settings.MRemoteNgStatus);
        Assert.Equal(3, vm.Tree.Roots.Count);
    }

    [AvaloniaFact]
    public void SidebarButtonOpensSettingsWithBackupButtons()
    {
        var (window, vm) = TestWindows.Open();

        window.Named<Button>("SettingsButton").Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Settings", vm.Sessions.Selected?.Title);
        var view = window.GetVisualDescendants().OfType<Views.SettingsView>().Single();
        var settings = (SettingsViewModel)vm.Sessions.Selected!;

        // Genel sekmesi önde (şimdilik boş).
        Assert.True(settings.ShowGeneral);
        Assert.False(view.Named<Button>("ExportButton").IsEffectivelyVisible);
        Assert.False(view.Named<Button>("ImportButton").IsEffectivelyVisible);

        // Dışa aktar: açıklama ve tek düğme.
        settings.ShowExport = true;
        Dispatcher.UIThread.RunJobs();
        Assert.True(view.Named<Button>("ExportButton").IsEffectivelyVisible);
        Assert.False(view.Named<Button>("ImportButton").IsEffectivelyVisible);
        Assert.True(view.Named<TextBlock>("ExportHint").IsEffectivelyVisible);

        settings.ShowImport = true;
        Dispatcher.UIThread.RunJobs();
        Assert.False(view.Named<Button>("ExportButton").IsEffectivelyVisible);
        Assert.True(view.Named<Button>("ImportButton").IsEffectivelyVisible);
        Assert.True(view.Named<TextBlock>("ImportHint").IsEffectivelyVisible);
        Assert.False(view.Named<CheckBox>("DeleteOldBox").IsChecked);

        // İçe aktar alt alta iki bölüm: rdpNext dosyası ve mRemoteNG dosyası, her biri kendi düğmesiyle.
        var texts = view.Texts().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).ToList();
        Assert.True(texts.IndexOf("Import from rdpNext File") < texts.IndexOf("Import from mRemoteNG File"));
        Assert.True(view.Named<Button>("ImportMRemoteNgButton").IsEffectivelyVisible);
        Assert.True(view.Named<TextBlock>("MRemoteNgHint").IsEffectivelyVisible);
        var password = view.Named<TextBox>("MasterPasswordBox");
        Assert.Equal('•', password.PasswordChar);
        Assert.Contains("Master password", texts);
        Assert.Contains("Only needed if you set a master password for the file in mRemoteNG. Leave it empty otherwise.", texts);
    }
}

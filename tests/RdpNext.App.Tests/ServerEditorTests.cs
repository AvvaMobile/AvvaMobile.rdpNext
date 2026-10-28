using RdpNext.App.ViewModels;
using RdpNext.Core.Storage;

namespace RdpNext.App.Tests;

public class ServerEditorTests
{
    private readonly string _path = TestWindows.SampleFile();
    private readonly MainWindowViewModel _vm;

    public ServerEditorTests() => _vm = TestWindows.ViewModel(_path);

    private GroupNodeViewModel Folder(string name) => _vm.Tree.Node<GroupNodeViewModel>(
        _vm.Tree.Root.Children.OfType<Group>().First(g => g.Name == name).Id);

    private ServerNodeViewModel Server(string name) => _vm.Tree.Node<ServerNodeViewModel>(
        _vm.Tree.Root.AllServers().First(s => s.Name == name).Id);

    private ServerEditorViewModel NewIn(string folder)
    {
        _vm.Tree.Selected = Folder(folder);
        _vm.NewServer();
        return Assert.IsType<ServerEditorViewModel>(_vm.Sessions.Selected);
    }

    private Group Reload() => new ConnectionStore(_path).Load().Root;

    [Fact]
    public void NewServerSavesToTreeFileAndKeychain()
    {
        var editor = NewIn("Üretim");
        Assert.Equal("New server", editor.Title);
        editor.Name = "web-02";
        editor.Host = "10.0.1.50";
        editor.Password = "pw";

        editor.SaveCommand.Execute(null);

        var saved = Reload().AllServers().Single(s => s.Name == "web-02");
        Assert.Equal(("10.0.1.50", 3389, "Üretim"), (saved.Host, saved.Port, saved.Parent!.Name));
        Assert.Equal("pw", _vm.Secrets.GetPassword(saved.Id));
        Assert.DoesNotContain("\"pw\"", File.ReadAllText(_path));
        Assert.Contains(Folder("Üretim").Children, n => n.Name == "web-02");
        Assert.Equal("web-02 settings", editor.Title);
    }

    [Fact]
    public void NewServerDefaultsToWindows()
    {
        var editor = NewIn("Üretim");

        Assert.Equal((Protocol.Rdp, "3389", true, false), (editor.Protocol, editor.Port, editor.IsRdp, editor.IsSsh));
    }

    [Fact]
    public void ChoosingSshSetsPort22AndShowsSshFields()
    {
        var editor = NewIn("Üretim");

        editor.Protocol = Protocol.Ssh;

        Assert.Equal(("22", true, false), (editor.Port, editor.IsSsh, editor.IsRdp));
        Assert.Equal((true, false), (editor.IsPasswordAuth, editor.IsKeyAuth));
        editor.Auth = SshAuth.Key;
        Assert.Equal((false, true), (editor.IsPasswordAuth, editor.IsKeyAuth));
    }

    [Fact]
    public void CustomPortSurvivesTypeChange()
    {
        var editor = NewIn("Üretim");
        editor.Port = "2200";

        editor.Protocol = Protocol.Ssh;
        Assert.Equal("2200", editor.Port);
        editor.Protocol = Protocol.Rdp;
        Assert.Equal("2200", editor.Port);
    }

    [Fact]
    public void ChoosingSshLeavesAdvancedTab()
    {
        var editor = NewIn("Üretim");
        editor.ShowAdvanced = true;

        editor.Protocol = Protocol.Ssh;

        Assert.False(editor.ShowAdvanced);
    }

    private ServerEditorViewModel SavedSshKeyServer(string host, string passphrase = "")
    {
        var editor = NewIn("Üretim");
        editor.Protocol = Protocol.Ssh;
        editor.Host = host;
        editor.Auth = SshAuth.Key;
        editor.KeyFile = "/k/id_ed25519";
        editor.Password = passphrase;
        editor.SaveCommand.Execute(null);
        return editor;
    }

    [Fact]
    public void SshKeyServerSavesPathAndPassphrase()
    {
        SavedSshKeyServer("10.0.2.5", "anahtar123");

        var saved = Reload().AllServers().Single(s => s.Host == "10.0.2.5");
        Assert.Equal((Protocol.Ssh, 22, SshAuth.Key, "/k/id_ed25519", (string?)null), (saved.Protocol, saved.Port, saved.Auth, saved.KeyFile, saved.Domain));
        Assert.Equal("anahtar123", _vm.Secrets.GetPassword(saved.Id));
        Assert.DoesNotContain("anahtar123", File.ReadAllText(_path));
        Assert.Equal(ServerIcon.Ssh, Server("10.0.2.5").Icon);
    }

    [Fact]
    public void SshDoesNotSaveDomain()
    {
        var editor = NewIn("Üretim");
        editor.Domain = "AVVA";
        editor.Protocol = Protocol.Ssh;
        editor.Host = "10.0.2.6";

        editor.SaveCommand.Execute(null);

        Assert.Null(Reload().AllServers().Single(s => s.Host == "10.0.2.6").Domain);
    }

    [Fact]
    public void SwitchingBackToWindowsRestoresRdpDefaults()
    {
        var editor = SavedSshKeyServer("10.0.2.7");
        var server = Server("10.0.2.7").Server;
        server.HostKeyFingerprint = "SHA256:x";

        editor.Protocol = Protocol.Rdp;
        editor.SaveCommand.Execute(null);

        var saved = Reload().AllServers().Single(s => s.Host == "10.0.2.7");
        Assert.Equal((Protocol.Rdp, 3389, SshAuth.Password, (string?)null, (string?)null),
            (saved.Protocol, saved.Port, saved.Auth, saved.KeyFile, saved.HostKeyFingerprint));
        Assert.Equal(ServerIcon.Windows, Server("10.0.2.7").Icon);
    }

    [Fact]
    public void ChangingAuthMethodDeletesOldSecret()
    {
        var editor = NewIn("Üretim");
        editor.Protocol = Protocol.Ssh;
        editor.Host = "10.0.2.8";
        editor.Password = "sifre";
        editor.SaveCommand.Execute(null);
        var id = Server("10.0.2.8").Server.Id;
        Assert.Equal("sifre", _vm.Secrets.GetPassword(id));

        editor.Auth = SshAuth.Key;
        editor.SaveCommand.Execute(null);

        Assert.Null(_vm.Secrets.GetPassword(id));
        Assert.Equal("", editor.Password);
    }

    [Fact]
    public async Task ChooseKeyFileFillsPath()
    {
        var vm = new MainWindowViewModel(new ConnectionStore(TestWindows.SampleFile()), new Core.Secrets.InMemorySecretStore(),
            files: new KeyFiles("/Users/x/.ssh/id_ed25519"));
        vm.NewServer();
        var editor = Assert.IsType<ServerEditorViewModel>(vm.Sessions.Selected);
        editor.Protocol = Protocol.Ssh;
        editor.Auth = SshAuth.Key;

        await editor.ChooseKeyFileCommand.ExecuteAsync(null);

        Assert.Equal("/Users/x/.ssh/id_ed25519", editor.KeyFile);
    }

    private sealed class KeyFiles(string? path) : Services.IFileDialogService
    {
        public Task<string?> SaveAsync(string suggestedName) => Task.FromResult<string?>(null);

        public Task<string?> OpenAsync() => Task.FromResult<string?>(null);

        public Task<string?> OpenKeyFileAsync() => Task.FromResult(path);
    }

    [Fact]
    public void EmptyNameDefaultsToHost()
    {
        var editor = NewIn("Üretim");
        editor.Host = "10.0.1.60";

        editor.SaveCommand.Execute(null);

        Assert.Contains(Reload().AllServers(), s => s.Name == "10.0.1.60");
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("70000")]
    [InlineData("")]
    public void InvalidPortDisablesSave(string port)
    {
        var editor = NewIn("Üretim");
        editor.Host = "10.0.1.70";
        editor.Port = port;

        Assert.False(editor.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void EmptyHostDisablesSave()
    {
        var editor = NewIn("Üretim");
        editor.Host = "  ";

        Assert.False(editor.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void EditChangesNameAndMovesFolder()
    {
        _vm.EditServer(Server("web-01"));
        var editor = Assert.IsType<ServerEditorViewModel>(_vm.Sessions.Selected);
        Assert.Equal("web-01 settings", editor.Title);
        editor.Name = "web-01-yeni";
        editor.Folder = editor.Folders.First(f => f.Group?.Name == "Test");

        editor.SaveCommand.Execute(null);

        var saved = Reload().AllServers().Single(s => s.Name == "web-01-yeni");
        Assert.Equal("Test", saved.Parent!.Name);
        Assert.Contains(Folder("Test").Children, n => n.Name == "web-01-yeni");
        Assert.DoesNotContain(Folder("Üretim").Children, n => n.Name == "web-01-yeni");
    }

    [Fact]
    public void ClearingPasswordDeletesSecret()
    {
        var node = Server("web-01");
        _vm.Secrets.SetPassword(node.Server.Id, "eski");
        _vm.EditServer(node);
        var editor = Assert.IsType<ServerEditorViewModel>(_vm.Sessions.Selected);
        Assert.Equal("eski", editor.Password);

        editor.Password = "";
        editor.SaveCommand.Execute(null);

        Assert.Null(_vm.Secrets.GetPassword(node.Server.Id));
    }

    [Fact]
    public void ClosingUnsavedNewServerLeavesNothing()
    {
        var before = _vm.Tree.Root.AllServers().Count();
        var editor = NewIn("Üretim");
        editor.Host = "10.0.1.80";

        _vm.CloseTabCommand.Execute(editor);

        Assert.Equal(before, _vm.Tree.Root.AllServers().Count());
        Assert.Equal(before, Reload().AllServers().Count());
    }

    [Fact]
    public void HintsShowInheritedValues()
    {
        var uretim = Folder("Üretim").Group;
        uretim.Username = "admin";
        uretim.Domain = "AVVA";
        _vm.Secrets.SetPassword(uretim.Id, "g");

        var editor = NewIn("Üretim");

        Assert.Equal(("admin", "from Üretim"), (editor.UsernameHint, editor.UsernameSource));
        Assert.Equal(("AVVA", "from Üretim"), (editor.DomainHint, editor.DomainSource));
        Assert.Equal(("••••••", "from Üretim"), (editor.PasswordHint, editor.PasswordSource));

        editor.Folder = editor.Folders.First(f => f.Group?.Name == "Test");
        Assert.Null(editor.UsernameHint);
        Assert.Null(editor.UsernameSource);
    }

    [Fact]
    public void EditingSameServerTwiceReusesTab()
    {
        _vm.EditServer(Server("web-01"));
        _vm.EditServer(Server("sql-01"));
        _vm.EditServer(Server("web-01"));

        Assert.Equal(2, _vm.Sessions.Tabs.OfType<ServerEditorViewModel>().Count());
        Assert.Equal("web-01 settings", _vm.Sessions.Selected?.Title);
    }

    [Fact]
    public void NewServerWithNothingSelectedGoesToTop()
    {
        _vm.Tree.Selected = null;
        _vm.NewServer();
        var editor = Assert.IsType<ServerEditorViewModel>(_vm.Sessions.Selected);
        Assert.Null(editor.Folder.Group);
        Assert.Equal("Top level", editor.Folder.Label);
        editor.Host = "10.9.9.9";

        editor.SaveCommand.Execute(null);

        Assert.Contains(Reload().Children, n => n is Server { Host: "10.9.9.9" });
    }
}

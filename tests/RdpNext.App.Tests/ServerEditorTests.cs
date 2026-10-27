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
    public void IconIsSavedAndLoaded()
    {
        var editor = NewIn("Üretim");
        Assert.Equal(ServerIcon.Windows, editor.Icon);
        editor.Host = "10.0.1.70";
        editor.Icon = ServerIcon.Linux;

        editor.SaveCommand.Execute(null);

        Assert.Equal(ServerIcon.Linux, Reload().AllServers().Single(s => s.Host == "10.0.1.70").Icon);
        Assert.Equal(ServerIcon.Linux, Server("10.0.1.70").Icon);
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
        Assert.False(editor.ConnectCommand.CanExecute(null));
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
    public void ConnectSavesThenOpensSession()
    {
        var editor = NewIn("Üretim");
        editor.Host = "10.0.1.90";

        editor.ConnectCommand.Execute(null);

        Assert.Contains(Reload().AllServers(), s => s.Host == "10.0.1.90");
        var session = Assert.IsType<SessionTabViewModel>(_vm.Sessions.Selected);
        Assert.Equal("10.0.1.90", session.Title);
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

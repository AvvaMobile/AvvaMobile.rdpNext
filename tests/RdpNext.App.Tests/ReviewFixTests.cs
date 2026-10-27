using RdpNext.App.Services;
using RdpNext.App.ViewModels;
using RdpNext.Core.Secrets;
using RdpNext.Core.Storage;

namespace RdpNext.App.Tests;

public class ReviewFixTests
{
    private sealed class Yes : IConfirmService
    {
        public Task<bool> ConfirmAsync(string message, string confirmText) => Task.FromResult(true);

        public Task<string?> AskTextAsync(string message, string confirmText) => Task.FromResult<string?>("Yeni");
    }

    private sealed class DeniedKeychain : ISecretStore
    {
        public string? GetPassword(Guid nodeId) => throw new InvalidOperationException("Anahtar zinciri hatası: -128");
        public void SetPassword(Guid nodeId, string password) => throw new InvalidOperationException("Anahtar zinciri hatası: -128");
        public void DeletePassword(Guid nodeId) => throw new InvalidOperationException("Anahtar zinciri hatası: -128");
    }

    private sealed class CountingStore : ISecretStore
    {
        private readonly InMemorySecretStore _inner = new();
        public int Writes { get; private set; }
        public string? GetPassword(Guid nodeId) => _inner.GetPassword(nodeId);
        public void SetPassword(Guid nodeId, string password) { Writes++; _inner.SetPassword(nodeId, password); }
        public void DeletePassword(Guid nodeId) { Writes++; _inner.DeletePassword(nodeId); }
    }

    private readonly string _path = TestWindows.SampleFile();

    private MainWindowViewModel Create(ISecretStore? secrets = null) =>
        new(new ConnectionStore(_path), secrets ?? new InMemorySecretStore(), new Yes());

    private static GroupNodeViewModel Folder(MainWindowViewModel vm, string name) => vm.Tree.Node<GroupNodeViewModel>(
        vm.Tree.Root.Children.OfType<Group>().First(g => g.Name == name).Id);

    private static ServerNodeViewModel Server(MainWindowViewModel vm, string name) => vm.Tree.Node<ServerNodeViewModel>(
        vm.Tree.Root.AllServers().First(s => s.Name == name).Id);

    private Group Reload() => new ConnectionStore(_path).Load().Root;

    [Fact]
    public void OpenFolderEditorRefreshesListAfterAnotherFolderMoves()
    {
        var vm = Create();
        vm.EditFolder(Folder(vm, "Üretim"));
        var uretim = Assert.IsType<FolderEditorViewModel>(vm.Sessions.Selected);
        vm.EditFolder(Folder(vm, "Test"));
        var test = Assert.IsType<FolderEditorViewModel>(vm.Sessions.Selected);
        test.Parent = test.Folders.First(f => f.Group?.Name == "Üretim");
        test.SaveCommand.Execute(null);

        Assert.DoesNotContain(uretim.Folders, f => f.Group?.Name == "Test");
        Assert.Contains(Reload().Children, n => n.Name == "Üretim");
    }

    [Fact]
    public async Task DeletingFolderClearsSelectionInsideIt()
    {
        var vm = Create();
        vm.Tree.Selected = Server(vm, "web-01");

        await vm.DeleteNodeCommand.ExecuteAsync(Folder(vm, "Üretim"));
        vm.NewServer();

        Assert.Null(vm.Tree.Selected);
        Assert.Null(Assert.IsType<ServerEditorViewModel>(vm.Sessions.Selected).Folder.Group);
    }

    [Fact]
    public async Task EditorFolderResetsToTopWhenItsFolderIsDeleted()
    {
        var vm = Create();
        vm.Tree.Selected = Folder(vm, "Test");
        vm.NewServer();
        var editor = Assert.IsType<ServerEditorViewModel>(vm.Sessions.Selected);

        await vm.DeleteNodeCommand.ExecuteAsync(Folder(vm, "Test"));
        editor.Host = "10.7.7.7";
        editor.SaveCommand.Execute(null);

        Assert.Null(editor.Folder.Group);
        Assert.Contains(Reload().Children, n => n is Server { Host: "10.7.7.7" });
    }

    [Fact]
    public async Task DeletingSearchedServerClearsFirstMatch()
    {
        var vm = Create();
        vm.Tree.Query = "web-01";

        await vm.DeleteNodeCommand.ExecuteAsync(Server(vm, "web-01"));

        Assert.Null(vm.Tree.FirstMatch);
        Assert.False(Folder(vm, "Üretim").IsVisible);
    }

    [Fact]
    public void EditorOpensAndSavesWhenKeychainDenied()
    {
        var vm = Create(new DeniedKeychain());

        vm.EditServer(Server(vm, "web-01"));
        var editor = Assert.IsType<ServerEditorViewModel>(vm.Sessions.Selected);
        editor.Name = "web-01b";
        editor.Password = "pw";
        editor.SaveCommand.Execute(null);

        Assert.Contains(Reload().AllServers(), s => s.Name == "web-01b");
        Assert.StartsWith("Anahtar zincirine erişilemedi", vm.Notice);
    }

    [Fact]
    public void SavingWithUnchangedPasswordDoesNotTouchKeychain()
    {
        var secrets = new CountingStore();
        var vm = Create(secrets);
        var web = Server(vm, "web-01");
        secrets.SetPassword(web.Server.Id, "pw");
        var before = secrets.Writes;

        vm.EditServer(web);
        var editor = Assert.IsType<ServerEditorViewModel>(vm.Sessions.Selected);
        editor.Name = "web-01c";
        editor.SaveCommand.Execute(null);

        Assert.Equal(before, secrets.Writes);
    }
}

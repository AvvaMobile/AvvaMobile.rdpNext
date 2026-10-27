using RdpNext.App.Services;
using RdpNext.App.ViewModels;
using RdpNext.Core.Secrets;
using RdpNext.Core.Storage;

namespace RdpNext.App.Tests;

public class FolderEditorTests
{
    private readonly string _path = TestWindows.SampleFile();
    private readonly MainWindowViewModel _vm;

    private sealed class Answer(string name) : IConfirmService
    {
        public Task<bool> ConfirmAsync(string message, string confirmText) => Task.FromResult(true);

        public Task<string?> AskTextAsync(string message, string confirmText) => Task.FromResult<string?>(name);
    }

    public FolderEditorTests() => _vm = new MainWindowViewModel(new ConnectionStore(_path), new InMemorySecretStore(), new Answer("Web"));

    private GroupNodeViewModel Folder(string name) => _vm.Tree.Node<GroupNodeViewModel>(
        _vm.Tree.Root.Children.OfType<Group>().First(g => g.Name == name).Id);

    private Group Reload() => new ConnectionStore(_path).Load().Root;





    [Fact]
    public void FolderCannotMoveIntoItself()
    {
        _vm.EditFolder(Folder("Üretim"));
        var editor = Assert.IsType<FolderEditorViewModel>(_vm.Sessions.Selected);

        Assert.DoesNotContain(editor.Folders, f => f.Group?.Name == "Üretim");
        Assert.Contains(editor.Folders, f => f.Group?.Name == "Test");
    }

    [Fact]
    public void EditedFolderEmptyNameDisablesSave()
    {
        _vm.EditFolder(Folder("Test"));
        var editor = Assert.IsType<FolderEditorViewModel>(_vm.Sessions.Selected);
        editor.Name = "  ";

        Assert.False(editor.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void FolderCredentialsSaved()
    {
        _vm.EditFolder(Folder("Test"));
        var editor = Assert.IsType<FolderEditorViewModel>(_vm.Sessions.Selected);
        editor.Username = "admin";
        editor.Domain = "AVVA";
        editor.Password = "klasor-sifre";

        editor.SaveCommand.Execute(null);

        var test = Reload().Children.OfType<Group>().Single(g => g.Name == "Test");
        Assert.Equal(("admin", "AVVA"), (test.Username, test.Domain));
        Assert.Equal("klasor-sifre", _vm.Secrets.GetPassword(test.Id));
    }

    [Fact]
    public void FolderColorSaved()
    {
        _vm.EditFolder(Folder("Test"));
        var editor = Assert.IsType<FolderEditorViewModel>(_vm.Sessions.Selected);
        Assert.Equal(ServerColor.Blue, editor.Color);
        editor.Color = ServerColor.Teal;

        editor.SaveCommand.Execute(null);

        Assert.Equal(ServerColor.Teal, Folder("Test").Color);
        Assert.Equal(ServerColor.Teal, Reload().Children.OfType<Group>().Single(g => g.Name == "Test").Color);
    }

    [Fact]
    public void MovingFolderMovesItInTree()
    {
        _vm.EditFolder(Folder("Test"));
        var editor = Assert.IsType<FolderEditorViewModel>(_vm.Sessions.Selected);
        editor.Parent = editor.Folders.First(f => f.Group?.Name == "Üretim");

        editor.SaveCommand.Execute(null);

        Assert.Contains(Folder("Üretim").Children, n => n.Name == "Test");
        Assert.DoesNotContain(_vm.Tree.Roots, n => n.Name == "Test");
    }
}

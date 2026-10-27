using RdpNext.App.ViewModels;
using RdpNext.Core.Storage;

namespace RdpNext.App.Tests;

public class FolderEditorTests
{
    private readonly string _path = TestWindows.SampleFile();
    private readonly MainWindowViewModel _vm;

    public FolderEditorTests() => _vm = TestWindows.ViewModel(_path);

    private GroupNodeViewModel Folder(string name) => _vm.Tree.Node<GroupNodeViewModel>(
        _vm.Tree.Root.Children.OfType<Group>().First(g => g.Name == name).Id);

    private Group Reload() => new ConnectionStore(_path).Load().Root;

    private FolderEditorViewModel Open()
    {
        _vm.NewFolder();
        return Assert.IsType<FolderEditorViewModel>(_vm.Sessions.Selected);
    }

    [Fact]
    public void NewFolderAtTopLevel()
    {
        _vm.Tree.Selected = null;
        var editor = Open();
        Assert.Equal("Yeni klasör", editor.Title);
        editor.Name = "Muhasebe";

        editor.SaveCommand.Execute(null);

        Assert.Contains(Reload().Children, n => n is Group { Name: "Muhasebe" });
        Assert.Contains(_vm.Tree.Roots, n => n.Name == "Muhasebe");
        Assert.Equal("Muhasebe ayarları", editor.Title);
    }

    [Fact]
    public void NewFolderInsideSelected()
    {
        _vm.Tree.Selected = Folder("Üretim");
        var editor = Open();
        editor.Name = "Web";

        editor.SaveCommand.Execute(null);

        var uretim = Reload().Children.OfType<Group>().Single(g => g.Name == "Üretim");
        Assert.Contains(uretim.Children, n => n is Group { Name: "Web" });
    }

    [Fact]
    public void EmptyNameDisablesSave()
    {
        var editor = Open();
        editor.Name = "  ";

        Assert.False(editor.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void FolderCannotMoveIntoItself()
    {
        _vm.Tree.Selected = Folder("Üretim");
        var child = Open();
        child.Name = "Web";
        child.SaveCommand.Execute(null);

        _vm.EditFolder(Folder("Üretim"));
        var editor = Assert.IsType<FolderEditorViewModel>(_vm.Sessions.Selected);

        Assert.DoesNotContain(editor.Folders, f => f.Group?.Name is "Üretim" or "Web");
        Assert.Contains(editor.Folders, f => f.Group?.Name == "Test");
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

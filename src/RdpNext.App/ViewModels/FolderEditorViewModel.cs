using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RdpNext.Core.Model;

namespace RdpNext.App.ViewModels;

// Klasör ekleme ve düzenleme sekmesi; içindeki sunucuların devralacağı kimlik bilgileri burada.
public sealed partial class FolderEditorViewModel : TabViewModel
{
    private readonly MainWindowViewModel _owner;

    public FolderEditorViewModel(MainWindowViewModel owner, Group? existing, Group? parent)
    {
        _owner = owner;
        Existing = existing;
        _folders = owner.Tree.FolderOptions(exclude: existing);
        owner.Tree.StructureChanged += RebuildFolders;
        if (existing is not null)
        {
            _name = existing.Name;
            _username = existing.Username ?? "";
            _domain = existing.Domain ?? "";
            _password = _savedPassword = owner.Secrets.GetPassword(existing.Id) ?? "";
            parent = existing.Parent;
        }
        _parent = _folders.FirstOrDefault(f => f.Group == (parent == owner.Tree.Root ? null : parent)) ?? _folders[0];
    }

    public Group? Existing { get; private set; }

    public override string Title => Existing is null
        ? Strings.Strings.NewFolder
        : string.Format(Strings.Strings.SettingsTitle, Existing.Name);

    private string _savedPassword = "";

    [ObservableProperty]
    private IReadOnlyList<FolderOption> _folders;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _name = "";

    [ObservableProperty]
    private FolderOption _parent;

    [ObservableProperty]
    private string _username = "";

    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    private bool _isPasswordVisible;

    [ObservableProperty]
    private string _domain = "";

    public bool IsNew => Existing is null;

    private bool CanSave() => !string.IsNullOrWhiteSpace(Name);

    [RelayCommand]
    private Task Delete() => Existing is null
        ? Task.CompletedTask
        : _owner.DeleteNodeCommand.ExecuteAsync(_owner.Tree.Node<GroupNodeViewModel>(Existing.Id));

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        var tree = _owner.Tree;
        var group = Existing ?? new Group();
        group.Name = Name.Trim();
        group.Username = string.IsNullOrWhiteSpace(Username) ? null : Username.Trim();
        group.Domain = string.IsNullOrWhiteSpace(Domain) ? null : Domain.Trim();

        if (Existing is null)
        {
            tree.AddGroup(group, Parent.Group);
            Existing = group;
            OnPropertyChanged(nameof(IsNew));
        }
        else
        {
            var node = tree.Node<GroupNodeViewModel>(group.Id);
            if (group.Parent != (Parent.Group ?? tree.Root))
                tree.Move(node, Parent.Group);
            node.Refresh();
        }

        _owner.Persist();
        if (Password != _savedPassword)
        {
            if (string.IsNullOrEmpty(Password))
                _owner.Secrets.DeletePassword(group.Id);
            else
                _owner.Secrets.SetPassword(group.Id, Password);
            _savedPassword = Password;
        }
        TitleChanged();
    }

    private void RebuildFolders()
    {
        var selected = Parent.Group;
        Folders = _owner.Tree.FolderOptions(exclude: Existing);
        Parent = Folders.FirstOrDefault(f => f.Group == selected) ?? Folders[0];
    }

    public override void OnClosed() => _owner.Tree.StructureChanged -= RebuildFolders;
}

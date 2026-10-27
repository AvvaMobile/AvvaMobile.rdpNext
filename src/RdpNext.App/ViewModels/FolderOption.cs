using RdpNext.Core.Model;

namespace RdpNext.App.ViewModels;

// Klasör seçicideki satır: Group null ise en üst.
public sealed record FolderOption(Group? Group, string Label);

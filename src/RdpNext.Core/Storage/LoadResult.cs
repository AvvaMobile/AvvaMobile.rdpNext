using RdpNext.Core.Model;

namespace RdpNext.Core.Storage;

public sealed record LoadResult(Group Root, string? CorruptBackupPath);

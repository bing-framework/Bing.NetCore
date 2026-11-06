namespace Bing.Biz.FileManagement;

public sealed partial class FileManagementService
{
    /// <summary>单文件与列表共用读取授权；不缓存业务权限，也不在数据库事务内执行授权处理器。</summary>
    private async Task AuthorizeReadSnapshotAsync(FileRecord file, IReadOnlyList<FileBinding> bindings, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (file.Ownership == Bing.FileStorage.RemoteDrive.RemoteDriveOwnership.Personal)
        {
            if (file.CreatorId == _user.UserId) return;
            throw new FileManagementException("forbidden", "个人文件仅供所有者访问。");
        }
        if (await _permissions.IsGrantedAsync("files.manage", ct)) return;
        if (bindings.Count == 0 && file.CreatorId == _user.UserId) return;
        foreach (var binding in bindings)
        {
            ct.ThrowIfCancellationRequested();
            if (await _authorizer.AuthorizeAsync(binding.BusinessType, binding.BusinessId, FileBusinessAccess.Read, ct)) return;
        }
        throw new FileManagementException("forbidden", "无文件访问权限。");
    }

    /// <summary>null 表示第三方仓储使用原逐项读取路径；内置仓储只加载本批文件及关联。</summary>
    private async Task<IReadOnlyDictionary<Guid, FileReadSnapshot>?> ReadAuthorizationBatchAsync(string scopeKey,
        IReadOnlyList<FileRecord> candidates, CancellationToken ct)
    {
        if (_store is not IFileReadBatchStore batchStore) return null;
        var snapshots = await batchStore.ReadReadyFilesAsync(scopeKey, candidates.Select(file => file.Id).ToArray(), ct);
        return snapshots.ToDictionary(snapshot => snapshot.File.Id);
    }

    private async Task<FileRecord?> ReadAuthorizedCandidateAsync(string scopeKey, string? creatorId, Guid id,
        IReadOnlyDictionary<Guid, FileReadSnapshot>? snapshots, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        FileReadSnapshot? snapshot;
        if (snapshots != null) snapshots.TryGetValue(id, out snapshot);
        else
        {
            var entry = await _store.FindAsync(id, ct);
            snapshot = entry == null ? null : new FileReadSnapshot { File = entry.File, Bindings = entry.Bindings };
        }
        // 候选之后可能已删除或改变状态；同时约束第三方仓储返回的范围。
        if (snapshot == null || snapshot.File.ScopeKey != scopeKey || snapshot.File.State != FileState.Ready ||
            (creatorId != null && snapshot.File.CreatorId != creatorId)) return null;
        await AuthorizeReadSnapshotAsync(snapshot.File, snapshot.Bindings, ct);
        return snapshot.File;
    }
}

using Bing.FileStorage;

namespace Bing.Biz.FileManagement;

public sealed class FileUserContext
{
    public string ScopeKey { get; set; } = "";
    public string UserId { get; set; } = "";
    public string? TenantId { get; set; }
    public StorageScopeKind ScopeKind { get; set; }
    public StorageScope ToStorageScope() => ScopeKind == StorageScopeKind.Host ? StorageScope.Host : StorageScope.ForTenant(TenantId!);
}

public sealed partial class FileManagementService
{
    /// <summary>返回已认证的范围标识；管理入口必须显式传 manage=true。</summary>
    public async Task<FileUserContext> GetContextAsync(bool manage = false, CancellationToken ct = default)
    {
        var scope = await ScopeAsync(ct);
        if (manage) await RequirePermissionAsync("files.manage", ct);
        return new() { ScopeKey = scope.Key, UserId = _user.UserId, ScopeKind = scope.Kind, TenantId = scope.TenantId };
    }

    public async Task<IReadOnlyList<UploadSessionSummary>> ListSessionsAsync(FileState? state = null, bool includeExpired = false,
        int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var context = await GetContextAsync(ct: ct);
        if (state.HasValue && !Enum.IsDefined(typeof(FileState), state.Value)) throw new FileManagementException("invalid", "会话状态无效。");
        return await _store.QuerySessionsAsync(context.ScopeKey, context.UserId, state, includeExpired, Now, Math.Max(0, skip), Math.Clamp(take, 1, 100), ct);
    }

    public async Task<IReadOnlyList<FileView>> ListBusinessFilesAsync(string businessType, string businessId,
        int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var scope = await ScopeAsync(ct);
        ValidateBusiness(businessType, businessId);
        if (!await _authorizer.AuthorizeAsync(businessType, businessId, FileBusinessAccess.Read, ct))
            throw new FileManagementException("forbidden", "无目标业务读取权限。");
        var query = new FileCandidateQuery { ScopeKey = scope.Key, BusinessType = businessType, BusinessId = businessId, Take = 100 };
        var result = new List<FileView>(); var remaining = Math.Max(0, skip); take = Math.Clamp(take, 1, 100);
        while (true)
        {
            var candidates = await _store.QueryFilesAsync(query, ct);
            var snapshots = candidates.Count == 0
                ? null
                : await ReadAuthorizationBatchAsync(scope.Key, candidates, ct);
            foreach (var file in candidates)
            {
                ct.ThrowIfCancellationRequested();
                FileReadSnapshot? snapshot;
                if (snapshots == null)
                {
                    var entry = await _store.FindAsync(file.Id, ct);
                    snapshot = entry == null ? null : new FileReadSnapshot { File = entry.File, Bindings = entry.Bindings };
                }
                else snapshots.TryGetValue(file.Id, out snapshot);

                if (snapshot == null || snapshot.File.State != FileState.Ready || snapshot.File.ScopeKey != scope.Key ||
                    !snapshot.Bindings.Any(binding => binding.BusinessType == businessType && binding.BusinessId == businessId)) continue;
                if (snapshot.File.Ownership == Bing.FileStorage.RemoteDrive.RemoteDriveOwnership.Personal && snapshot.File.CreatorId != _user.UserId) continue;
                if (remaining > 0) { remaining--; continue; }
                result.Add(FileView.From(snapshot.File));
                if (result.Count == take) return result;
            }
            if (candidates.Count < query.Take) return result;
            query.AfterCreatedAt = candidates[^1].CreatedAt; query.AfterKey = candidates[^1].RequestKey;
        }
    }

    public async Task<IReadOnlyList<FileBinding>> ListBindingsAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await AuthorizeReadAsync(id, ct: ct);
        var result = new List<FileBinding>();
        foreach (var binding in entry.Bindings)
            if (await _authorizer.AuthorizeAsync(binding.BusinessType, binding.BusinessId, FileBusinessAccess.Read, ct)) result.Add(binding);
        return result;
    }

    /// <summary>有界扫描；NextPosition 表示还有候选可扫描，空 Items 不等于查询结束。</summary>
    public async Task<FileSearchResult> SearchAsync(FileSearchRequest request, FileScanPosition? position = null, CancellationToken ct = default)
    {
        var context = await GetContextAsync(request.Manage, ct);
        if (request.Name?.Length > 255 || request.Container?.Length > 64 || request.ContentType?.Length > 256 ||
            request.CreatedFrom < 0 || request.CreatedTo < 0 || request.CreatedFrom > request.CreatedTo)
            throw new FileManagementException("invalid", "筛选条件无效。");
        var take = Math.Clamp(request.Take, 1, 100);
        var query = new FileCandidateQuery { ScopeKey = context.ScopeKey, CreatorId = request.Manage ? null : context.UserId,
            Name = request.Name, Container = request.Container, ContentType = request.ContentType,
            CreatedFrom = request.CreatedFrom, CreatedTo = request.CreatedTo, AfterCreatedAt = position?.CreatedAt, AfterKey = position?.Key, Take = 100 };
        var result = new FileSearchResult(); var items = new List<FileView>(); result.Items = items;
        for (var batch = 0; batch < 10; batch++)
        {
            var candidates = await _store.QueryFilesAsync(query, ct);
            var snapshots = await ReadAuthorizationBatchAsync(context.ScopeKey, candidates, ct);
            foreach (var candidate in candidates)
            {
                ct.ThrowIfCancellationRequested();
                result.NextPosition = new() { CreatedAt = candidate.CreatedAt, Key = candidate.RequestKey };
                try
                {
                    var file = await ReadAuthorizedCandidateAsync(context.ScopeKey, query.CreatorId, candidate.Id, snapshots, ct);
                    if (file != null) items.Add(FileView.From(file));
                }
                catch (FileManagementException ex) when (ex.Code is "forbidden" or "not_found") { }
                if (items.Count == take) return result;
            }
            if (candidates.Count < query.Take) { result.NextPosition = null; return result; }
            query.AfterCreatedAt = result.NextPosition!.CreatedAt; query.AfterKey = result.NextPosition.Key;
        }
        return result;
    }

    private static void ValidateBusiness(string type, string id)
    {
        if (string.IsNullOrWhiteSpace(type) || type.Length > 100 || string.IsNullOrWhiteSpace(id) || id.Length > 200)
            throw new FileManagementException("invalid", "业务类型或标识无效。");
    }
}

using System.Security.Cryptography;
using System.Text;
using Bing.FileStorage;
using Bing.MultiTenancy;
using Bing.Users;

namespace Bing.Biz.FileManagement;

public sealed class CreateFileUpload
{
    public Guid RequestId { get; set; }
    public string Container { get; set; } = "attachments";
    public string FileName { get; set; } = "";
    public long Length { get; set; }
    public string ContentType { get; set; } = "application/octet-stream";
    public long LastModified { get; set; }
}
public sealed class UploadSessionView
{
    public Guid Id { get; set; }
    public Guid FileId { get; set; }
    public FileState State { get; set; }
    public long Length { get; set; }
    public int PartSize { get; set; }
    public long LastModified { get; set; }
    public long ExpiresAt { get; set; }
    public bool DirectUpload { get; set; }
    public IReadOnlyList<FilePart> Parts { get; set; } = Array.Empty<FilePart>();
}
public sealed class PartUploadRequest
{
    public int Number { get; set; }
    public string Sha256 { get; set; } = "";
}
public sealed class FileView
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = "";
    public string Container { get; set; } = "";
    public long Length { get; set; }
    public string ContentType { get; set; } = "";
    public FileState State { get; set; }
    public FileVisibility Visibility { get; set; }
    public long CreatedAt { get; set; }
    public static FileView From(FileRecord f) => new() { Id = f.Id, FileName = f.FileName, Container = f.Container,
        Length = f.Length, ContentType = f.ContentType, State = f.State, Visibility = f.Visibility, CreatedAt = f.CreatedAt };
}

/// <summary>面向当前身份的附件入口；后台任务和迁移使用显式持久位置，不伪造当前用户。</summary>
public sealed partial class FileManagementService
{
    private readonly IFileManagementStore _store;
    private readonly IFileStorageFactory _storage;
    private readonly ICurrentUser _user;
    private readonly ICurrentTenant _tenant;
    private readonly IFilePermissionChecker _permissions;
    private readonly IFileAccessAuthorizer _authorizer;
    private readonly FileManagementOptions _options;
    public FileManagementService(IFileManagementStore store, IFileStorageFactory storage, ICurrentUser user, ICurrentTenant tenant,
        IFilePermissionChecker permissions, IFileAccessAuthorizer authorizer, FileManagementOptions options)
    { _store = store; _storage = storage; _user = user; _tenant = tenant; _permissions = permissions; _authorizer = authorizer; _options = options; }
    internal static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    internal static string HashKey(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private void Identity()
    {
        if (!_user.IsAuthenticated || string.IsNullOrWhiteSpace(_user.UserId)) throw new FileManagementException("unauthenticated", "需要登录。");
    }
    private async Task<StorageScope> ScopeAsync(CancellationToken ct)
    {
        Identity();
        if (_tenant.IsAvailable && !string.IsNullOrEmpty(_tenant.Id)) return StorageScope.ForTenant(_tenant.Id);
        await RequirePermissionAsync("files.host", ct);
        return StorageScope.Host;
    }
    public async Task RequirePermissionAsync(string permission, CancellationToken ct = default)
    {
        Identity();
        if (!await _permissions.IsGrantedAsync(permission, ct)) throw new FileManagementException("forbidden", "缺少文件操作权限。");
    }
    private async Task<FileEntry> ScopedAsync(Guid fileId, CancellationToken ct)
    {
        var scope = await ScopeAsync(ct);
        var entry = await _store.FindAsync(fileId, ct);
        if (entry == null || entry.File.ScopeKey != scope.Key) throw new FileManagementException("not_found", "文件不存在。");
        return entry;
    }
    private async Task<FileEntry> OwnedSessionAsync(Guid sessionId, bool uploading, CancellationToken ct)
    {
        var scope = await ScopeAsync(ct);
        var entry = await _store.FindSessionAsync(sessionId, ct);
        if (entry?.Session == null || entry.File.ScopeKey != scope.Key || entry.Session.OwnerId != _user.UserId)
            throw new FileManagementException("not_found", "上传会话不存在。");
        if (uploading && (entry.File.State != FileState.Uploading || entry.Session.ExpiresAt <= Now || entry.Session.AbsoluteExpiresAt <= Now))
            throw new FileManagementException("conflict", "上传会话已过期或不再接受分片。");
        return entry;
    }
    public async Task<UploadSessionView> CreateUploadAsync(CreateFileUpload request, CancellationToken ct = default)
    {
        var scope = await ScopeAsync(ct);
        await RequirePermissionAsync("files.upload", ct);
        if (!_options.Containers.TryGetValue(request.Container, out var policy)) throw new FileManagementException("invalid", "未配置的容器。");
        if (policy.Visibility == FileVisibility.Public) await RequirePermissionAsync("files.publish", ct);
        if (request.RequestId == Guid.Empty || string.IsNullOrWhiteSpace(request.FileName) || request.FileName.Length > 255 ||
            request.FileName.Any(c => char.IsControl(c) || c == '/' || c == '\\') || request.Length < 0 ||
            request.Length > policy.MaxFileLength || request.LastModified < 0)
            throw new FileManagementException("invalid", "文件名、长度或请求标识无效。");
        FileContentInspector.ValidateDeclaration(policy, request.FileName, request.ContentType, request.Length);
        var key = HashKey(scope.Key + "\n" + _user.UserId + "\n" + request.RequestId.ToString("N"));
        var existing = await _store.FindRequestAsync(key, ct);
        if (existing != null) return MatchRequest(existing, request);
        var backend = _storage.GetStorage(_storage.DefaultStore, scope);
        if (!backend.Capabilities.HasFlag(FileStorageCapabilities.Multipart)) throw new FileManagementException("configuration", "默认存储不支持分片。");
        var now = Now; var id = Guid.NewGuid();
        var entry = new FileEntry
        {
            File = new FileRecord { Id = id, RequestKey = key, ScopeKind = scope.Kind, TenantId = scope.TenantId, ScopeKey = scope.Key,
                StoreName = backend.Name, Container = request.Container, ObjectKey = id.ToString("N"), FileName = request.FileName,
                Length = request.Length, ContentType = new FileWriteRequest(new FileAddress(request.Container, id.ToString("N")), request.Length,
                    FileWriteMode.Overwrite, request.ContentType).ContentType, Visibility = policy.Visibility,
                CreatorId = _user.UserId, CreatedAt = now, State = FileState.Pending, Version = 1,
                ContentPolicyJson = System.Text.Json.JsonSerializer.Serialize(policy) },
            Session = new FileUploadSession { Id = Guid.NewGuid(), FileId = id, OwnerId = _user.UserId, TotalLength = request.Length,
                PartSize = _options.PartSize, State = FileState.Pending, WriteMode = backend.Capabilities.HasFlag(FileStorageCapabilities.CreateOnly) ? FileWriteMode.CreateOnly : FileWriteMode.Overwrite,
                LastActivityAt = now, ClientLastModified = request.LastModified, ExpiresAt = now + (long)_options.SessionIdleTimeout.TotalMilliseconds,
                AbsoluteExpiresAt = now + (long)_options.SessionMaxLifetime.TotalMilliseconds, Version = 1 },
            Admission = _options.Admission
        };
        if (!await _store.TryCreateAsync(entry, new[] { Operation(entry.File.Id, FileOperationKind.Initialize) }, ct))
        {
            existing = await _store.FindRequestAsync(key, ct) ?? throw new FileManagementException("conflict", "重复创建冲突，请重试。");
            return MatchRequest(existing, request);
        }
        return View(entry);
    }
    private UploadSessionView MatchRequest(FileEntry entry, CreateFileUpload request)
    {
        if (entry.Session == null || entry.File.Length != request.Length || entry.File.FileName != request.FileName ||
            entry.File.Container != request.Container || entry.File.ContentType != request.ContentType || entry.Session.ClientLastModified != request.LastModified)
            throw new FileManagementException("conflict", "同一个请求标识不能用于不同文件。");
        return View(entry);
    }
    private UploadSessionView View(FileEntry entry, IReadOnlyList<FilePart>? parts = null) => new()
    {
        Id = entry.Session!.Id, FileId = entry.File.Id, State = entry.File.State, Length = entry.File.Length, PartSize = entry.Session.PartSize,
        LastModified = entry.Session.ClientLastModified, ExpiresAt = entry.Session.ExpiresAt,
        DirectUpload = _storage.GetStorage(entry.File.StoreName, entry.File.Scope()).Capabilities.HasFlag(FileStorageCapabilities.SignedParts),
        Parts = parts ?? Array.Empty<FilePart>()
    };
    public async Task<UploadSessionView> GetSessionAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await OwnedSessionAsync(id, false, ct);
        if (entry.File.State == FileState.Uploading && entry.File.Length != 0)
        {
            var backend = Storage(entry);
            var actual = await ((IMultipartFileStorage)backend).ListPartsAsync(entry.Upload(), ct);
            var expected = (await _store.ListPartsAsync(id, ct)).ToDictionary(p => p.Number);
            return View(entry, actual.Where(p => expected.TryGetValue(p.Number, out var e) && PartConfirmed(backend, e, p))
                .Select(p => new FilePart { Number = p.Number, Length = p.Length, ETag = p.ETag, Sha256 = expected[p.Number].Sha256 }).ToArray());
        }
        return View(entry, (await _store.ListPartsAsync(id, ct)).Where(p => p.ETag != null).Select(p => p.ToStoragePart()).ToArray());
    }
    private IFileStorage Storage(FileEntry entry) => _storage.GetStorage(entry.File.StoreName, entry.File.Scope());
    public async Task<SignedFileRequest?> CreatePartRequestAsync(Guid id, PartUploadRequest part, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(part.Sha256)) throw new FileManagementException("invalid", "必须提供分片 SHA-256。");
        FileStorageStreams.ValidateSha256(part.Sha256);
        for (var attempt = 0; attempt < 12; attempt++)
        {
            var entry = await OwnedSessionAsync(id, true, ct);
            var length = entry.Upload().GetPartLength(part.Number);
            var old = await _store.FindPartAsync(id, part.Number, ct);
            if (old != null && old.Sha256 != part.Sha256) throw new FileManagementException("conflict", "已登记分片的摘要不同，请重新创建上传。");
            Touch(entry);
            var persisted = new FileUploadPart { SessionId = id, Number = part.Number, Length = length, Sha256 = part.Sha256 };
            // 同摘要重登记只刷新会话活动时间，不能用空 ETag 覆盖已验证的上传回执。
            if (!await _store.TryUpdateAsync(entry, entry.File.Version, old == null ? new[] { persisted } : null, ct: ct)) continue;
            var backend = Storage(entry);
            return backend.Capabilities.HasFlag(FileStorageCapabilities.SignedParts)
                ? await ((ISignedFileRequestProvider)backend).SignPartAsync(entry.Upload(), part.Number, part.Sha256, _options.PartSignatureLifetime, ct)
                : null;
        }
        throw new FileManagementException("conflict", "分片登记竞争，请重试。");
    }
    private void Touch(FileEntry entry)
    {
        entry.Session!.LastActivityAt = Now;
        entry.Session.ExpiresAt = Math.Min(entry.Session.AbsoluteExpiresAt, Now + (long)_options.SessionIdleTimeout.TotalMilliseconds);
    }
    public async Task<FilePart> UploadPartAsync(Guid id, int number, Stream source, CancellationToken ct = default)
    {
        var entry = await OwnedSessionAsync(id, true, ct);
        var backend = Storage(entry);
        if (backend.Capabilities.HasFlag(FileStorageCapabilities.SignedParts)) throw new FileManagementException("invalid", "该会话应使用分片直传地址。");
        var part = await _store.FindPartAsync(id, number, ct)
            ?? throw new FileManagementException("invalid", "请先登记分片摘要。");
        await using var permit = await new FileResourceGate(_store, _options).TryEnterAsync(new[] { backend.Name }, false, ct)
            ?? throw new FileManagementException("capacity", "当前存储上传并发已满，请稍后重试。");
        var receipt = await ((IMultipartFileStorage)backend).UploadPartAsync(entry.Upload(), number, source, part.Sha256, permit.Token);
        if (backend.Capabilities.HasFlag(FileStorageCapabilities.ServerVerifiedParts))
            await SavePartReceiptAsync(entry, backend, part, receipt, permit.Token);
        return receipt;
    }

    /// <summary>网络传输结束后使用新快照和短事务保存回执；不能跨传输持有数据库事务。</summary>
    private async Task SavePartReceiptAsync(FileEntry uploadedEntry, IFileStorage backend, FileUploadPart expected,
        FilePart receipt, CancellationToken ct)
    {
        if (receipt.Number != expected.Number || receipt.Length != expected.Length || receipt.Sha256 != expected.Sha256 ||
            string.IsNullOrWhiteSpace(receipt.ETag))
            throw new FileManagementException("invalid_parts", "实际上传回执与登记分片不一致。");
        for (var attempt = 0; attempt < 12; attempt++)
        {
            var entry = await OwnedSessionAsync(uploadedEntry.Session!.Id, true, ct);
            if (entry.Session!.BackendUploadId != uploadedEntry.Session.BackendUploadId)
                throw new FileManagementException("conflict", "上传会话已被替换。");
            // 旧请求可能晚于重传返回；每次 CAS 前核对当前后端版本，禁止重放旧回执。
            var remote = (await ((IMultipartFileStorage)backend).ListPartsAsync(entry.Upload(), ct))
                .SingleOrDefault(p => p.Number == receipt.Number);
            if (remote == null || remote.Length != receipt.Length || remote.ETag != receipt.ETag ||
                (remote.Sha256 != null && remote.Sha256 != receipt.Sha256))
                throw new FileManagementException("conflict", "远端分片已变化，请重新上传此分片。");
            if (entry.Session.ExpiresAt <= Now || entry.Session.AbsoluteExpiresAt <= Now)
                throw new FileManagementException("conflict", "上传会话已过期。");
            var saved = new FileUploadPart { SessionId = entry.Session.Id, Number = receipt.Number,
                Length = receipt.Length, Sha256 = receipt.Sha256!, ETag = receipt.ETag };
            Touch(entry);
            if (await _store.TryUpdateAsync(entry, entry.File.Version, new[] { saved }, ct: ct)) return;
        }
        throw new FileManagementException("conflict", "分片回执保存竞争，请重试。");
    }

    private static bool PartConfirmed(IFileStorage backend, FileUploadPart expected, FilePart actual)
    {
        if (expected.Number != actual.Number || expected.Length != actual.Length) return false;
        if (!backend.Capabilities.HasFlag(FileStorageCapabilities.ServerVerifiedParts)) return expected.Sha256 == actual.Sha256;
        return !string.IsNullOrWhiteSpace(expected.ETag) && expected.ETag == actual.ETag &&
            (actual.Sha256 == null || expected.Sha256 == actual.Sha256);
    }
    public async Task<UploadSessionView> CompleteUploadAsync(Guid id, CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var entry = await OwnedSessionAsync(id, false, ct);
            if (entry.File.State is FileState.Completing or FileState.Quarantined or FileState.Ready) return View(entry);
            if (entry.File.State != FileState.Uploading || entry.Session!.ExpiresAt <= Now)
                throw new FileManagementException("conflict", "会话尚未就绪、已过期或已结束。");
            var parts = await ConfirmPartsAsync(entry, _store, Storage(entry), ct);
            entry.File.State = entry.Session.State = FileState.Completing;
            if (await _store.TryUpdateAsync(entry, entry.File.Version, parts, new[] { Operation(entry.File.Id, FileOperationKind.Complete) }, ct))
                return View(entry);
        }
        throw new FileManagementException("conflict", "完成请求竞争，请重试。");
    }
    internal static async Task<IReadOnlyList<FileUploadPart>> ConfirmPartsAsync(FileEntry entry, IFileManagementStore store, IFileStorage backend, CancellationToken ct)
    {
        if (entry.File.Length == 0) return Array.Empty<FileUploadPart>();
        var actual = await ((IMultipartFileStorage)backend).ListPartsAsync(entry.Upload(), ct);
        FileStorageStreams.ValidateParts(entry.Upload(), actual);
        var expected = (await store.ListPartsAsync(entry.Session!.Id, ct)).ToDictionary(p => p.Number);
        foreach (var part in actual)
        {
            if (!expected.TryGetValue(part.Number, out var saved) || !PartConfirmed(backend, saved, part))
                throw new FileManagementException("invalid_parts", "分片内容与登记摘要不一致。");
            saved.ETag = part.ETag;
        }
        if (expected.Count != actual.Count) throw new FileManagementException("invalid_parts", "分片数量不一致。");
        return actual.Select(p => expected[p.Number]).ToArray();
    }
    public async Task AbortUploadAsync(Guid id, CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var entry = await OwnedSessionAsync(id, false, ct);
            if (entry.File.State == FileState.Aborted) return;
            if (entry.File.State is not (FileState.Pending or FileState.Uploading or FileState.Quarantined or FileState.Failed))
                throw new FileManagementException("conflict", "正在完成或已经完成的文件不能中止。");
            entry.File.State = entry.Session!.State = FileState.Aborted;
            if (await _store.TryUpdateAsync(entry, entry.File.Version, operations: new[] { Operation(entry.File.Id, FileOperationKind.Abort) }, ct: ct)) return;
        }
        throw new FileManagementException("conflict", "中止竞争，请重试。");
    }
    public async Task<FileEntry> AuthorizeReadAsync(Guid id, bool publicAccess = false, CancellationToken ct = default)
    {
        var entry = publicAccess ? await _store.FindAsync(id, ct) : await ScopedAsync(id, ct);
        if (entry == null || entry.File.State != FileState.Ready || (publicAccess && entry.File.Visibility != FileVisibility.Public))
            throw new FileManagementException("not_found", "文件不可访问。");
        if (publicAccess) return entry;
        if (await _permissions.IsGrantedAsync("files.manage", ct)) return entry;
        if (entry.Bindings.Count == 0 && entry.File.CreatorId == _user.UserId) return entry;
        foreach (var binding in entry.Bindings)
            if (await _authorizer.AuthorizeAsync(binding.BusinessType, binding.BusinessId, FileBusinessAccess.Read, ct)) return entry;
        throw new FileManagementException("forbidden", "无文件访问权限。");
    }
    public async Task<FileView> GetAsync(Guid id, CancellationToken ct = default) => FileView.From((await AuthorizeReadAsync(id, ct: ct)).File);
    /// <summary>
    /// 按可见文件分页；skip 只计入通过当前读取授权的 Ready 文件。
    /// 分批扫描候选，避免不可见记录占用页容量或一次载入全部文件。
    /// </summary>
    public async Task<IReadOnlyList<FileView>> ListAsync(int skip = 0, int take = 50, bool manage = false, CancellationToken ct = default)
    {
        var scope = await ScopeAsync(ct);
        if (manage) await RequirePermissionAsync("files.manage", ct);
        var remainingSkip = Math.Max(0, skip);
        var pageSize = Math.Clamp(take, 1, 100);
        const int batchSize = 100;
        var candidateSkip = 0;
        var result = new List<FileView>(pageSize);
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var candidates = await _store.ListReadyFilesAsync(scope.Key, manage ? null : _user.UserId, candidateSkip, batchSize, ct);
            foreach (var candidate in candidates)
            {
                ct.ThrowIfCancellationRequested();
                FileEntry entry;
                try { entry = await AuthorizeReadAsync(candidate.Id, ct: ct); }
                catch (FileManagementException ex) when (ex.Code == "forbidden" || ex.Code == "not_found") { continue; }
                if (remainingSkip > 0) { remainingSkip--; continue; }
                result.Add(FileView.From(entry.File));
                if (result.Count == pageSize) return result;
            }
            if (candidates.Count < batchSize) return result;
            // 数据库偏移与可见结果偏移分别推进，权限拒绝不能导致提前返回空页。
            candidateSkip = checked(candidateSkip + candidates.Count);
        }
    }
    public async Task<FileBinding> BindAsync(Guid id, string businessType, string businessId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(businessType) || businessType.Length > 100 || string.IsNullOrWhiteSpace(businessId) || businessId.Length > 200)
            throw new FileManagementException("invalid", "业务类型或标识无效。");
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var entry = await AuthorizeReadAsync(id, ct: ct);
            if (!await _authorizer.AuthorizeAsync(businessType, businessId, FileBusinessAccess.Bind, ct))
                throw new FileManagementException("forbidden", "无目标业务关联权限。");
            var old = entry.Bindings.FirstOrDefault(b => b.BusinessType == businessType && b.BusinessId == businessId);
            if (old != null) return old;
            if (entry.Bindings.Count >= 1000) throw new FileManagementException("invalid", "关联数量超过限制。");
            var binding = new FileBinding { Id = Guid.NewGuid(), FileId = id, BusinessType = businessType, BusinessId = businessId, CreatorId = _user.UserId, CreatedAt = Now };
            entry.Bindings.Add(binding);
            if (await _store.TryUpdateAsync(entry, entry.File.Version, ct: ct)) return binding;
        }
        throw new FileManagementException("conflict", "关联竞争，请重试。");
    }
    public async Task UnbindAsync(Guid id, Guid bindingId, CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var entry = await ScopedAsync(id, ct);
            if (entry.File.State != FileState.Ready) throw new FileManagementException("conflict", "文件不可修改。");
            var binding = entry.Bindings.FirstOrDefault(b => b.Id == bindingId);
            if (binding == null) { await AuthorizeReadAsync(id, ct: ct); return; }
            if (!await _authorizer.AuthorizeAsync(binding.BusinessType, binding.BusinessId, FileBusinessAccess.Unbind, ct))
                throw new FileManagementException("forbidden", "无业务解除关联权限。");
            entry.Bindings.Remove(binding);
            if (await _store.TryUpdateAsync(entry, entry.File.Version, ct: ct)) return;
        }
        throw new FileManagementException("conflict", "解除关联竞争，请重试。");
    }
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var entry = await ScopedAsync(id, ct);
            if (entry.File.CreatorId != _user.UserId && !await _permissions.IsGrantedAsync("files.manage", ct))
                throw new FileManagementException("forbidden", "无删除权限。");
            if (entry.Bindings.Count != 0) throw new FileManagementException("bound", "仍有关联，不能删除文件。");
            if (entry.File.State is FileState.DeletePending or FileState.Deleted) return;
            if (entry.File.State != FileState.Ready) throw new FileManagementException("conflict", "文件不可删除。");
            entry.File.State = FileState.DeletePending;
            if (await _store.TryUpdateAsync(entry, entry.File.Version, operations: new[] { Operation(id, FileOperationKind.Delete) }, ct: ct)) return;
        }
        throw new FileManagementException("conflict", "删除竞争，请重试。");
    }
    internal static FileOperation Operation(Guid fileId, FileOperationKind kind, string payload = "", long? due = null) => new()
    { Id = Guid.NewGuid(), FileId = fileId, Kind = kind, Payload = payload, State = FileOperationState.Pending, NextAttemptAt = due ?? Now, CreatedAt = Now, Version = 1 };
}

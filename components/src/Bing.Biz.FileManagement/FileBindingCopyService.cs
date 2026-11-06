using System.Text.Json;
using Bing.FileStorage;
using Bing.FileStorage.RemoteDrive;
using Bing.MultiTenancy;
using Bing.Users;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bing.Biz.FileManagement;

/// <summary>复制与关联分开：后台只生成私有副本，绑定始终使用当前真实身份授权。</summary>
public sealed class FileBindingCopyService
{
    private readonly IFileManagementStore _store;
    private readonly IFileStorageTargetStore _copies;
    private readonly FileManagementService _files;
    private readonly IFileStorageFactory _storage;
    private readonly IFileUploadStoreSelector _selector;
    private readonly IFileAccessAuthorizer _authorizer;
    private readonly ICurrentUser _user;
    private readonly ICurrentTenant _tenant;
    public FileBindingCopyService(IFileManagementStore store, IFileStorageTargetStore copies, FileManagementService files,
        IFileStorageFactory storage, IFileUploadStoreSelector selector, IFileAccessAuthorizer authorizer, ICurrentUser user, ICurrentTenant tenant)
    { _store = store; _copies = copies; _files = files; _storage = storage; _selector = selector; _authorizer = authorizer; _user = user; _tenant = tenant; }
    public async Task<FileBindingCopyRequest> CreateAsync(Guid fileId, Guid requestId, string businessType, string businessId, CancellationToken ct = default)
    {
        if (requestId == Guid.Empty || string.IsNullOrWhiteSpace(businessType) || businessType.Length > 100 || string.IsNullOrWhiteSpace(businessId) || businessId.Length > 200)
            throw new FileManagementException("invalid", "复制请求或业务标识无效。");
        var source = await _files.AuthorizeReadAsync(fileId, ct: ct);
        if (source.File.Ownership != RemoteDriveOwnership.Personal || source.File.CreatorId != _user.UserId)
            throw new FileManagementException("invalid", "复制发布只用于本人个人文件。");
        if (!await _authorizer.AuthorizeAsync(businessType, businessId, FileBusinessAccess.Bind, ct)) throw new FileManagementException("forbidden", "无目标业务关联权限。");
        var key = FileManagementService.HashKey(source.File.ScopeKey + "\n" + _user.UserId + "\ncopy\n" + requestId.ToString("N"));
        var existing = await _copies.FindCopyRequestAsync(key, ct);
        if (existing != null) return Match(existing, fileId, businessType, businessId);
        var chosen = await _selector.SelectAsync(source.File.Scope(), _user.UserId, source.File.Container, null, true, ct);
        var backend = _storage.GetStorage(chosen.StoreName, source.File.Scope());
        var limits = backend is IFileStorageLimitProvider provider ? await provider.GetLimitsAsync(ct) : null;
        if (limits != null && source.File.Length > limits.MaximumFileLength) throw new FileManagementException("invalid", "业务公共目标容量不足。");
        var copy = new FileEntry { File = new FileRecord { Id = Guid.NewGuid(), RequestKey = "copy-" + key, ScopeKey = source.File.ScopeKey,
            ScopeKind = source.File.ScopeKind, TenantId = source.File.TenantId, StoreName = chosen.StoreName, ConnectionId = chosen.ConnectionId,
            Ownership = RemoteDriveOwnership.Shared, Container = source.File.Container, ObjectKey = Guid.NewGuid().ToString("N"),
            FileName = source.File.FileName, Length = source.File.Length, ContentType = source.File.ContentType, ContentPolicyJson = source.File.ContentPolicyJson,
            Visibility = FileVisibility.Private, State = FileState.Pending, CreatorId = _user.UserId, CreatedAt = FileManagementService.Now, Version = 1 } };
        var request = new FileBindingCopyRequest { Id = Guid.NewGuid(), RequestKey = key, ScopeKey = source.File.ScopeKey, OwnerId = _user.UserId,
            SourceFileId = fileId, SourceVersion = source.File.Version, CopyFileId = copy.File.Id, BusinessType = businessType, BusinessId = businessId,
            CreatedAt = FileManagementService.Now };
        if (!await _copies.CreateCopyAsync(request, copy, FileManagementService.Operation(copy.File.Id, FileOperationKind.CopyForBinding, request.Id.ToString("N")), ct))
            return Match(await _copies.FindCopyRequestAsync(key, ct) ?? throw new FileManagementException("conflict", "复制创建竞争。"), fileId, businessType, businessId);
        return request;
    }
    private static FileBindingCopyRequest Match(FileBindingCopyRequest request, Guid fileId, string type, string id)
    {
        if (request.SourceFileId != fileId || request.BusinessType != type || request.BusinessId != id) throw new FileManagementException("conflict", "相同请求标识不能用于其他复制。");
        return request;
    }
    public async Task<FileBindingCopyRequest> GetAsync(Guid id, CancellationToken ct = default)
    {
        if (!_user.IsAuthenticated || string.IsNullOrEmpty(_user.UserId)) throw new FileManagementException("unauthenticated", "需要登录。");
        var scope = _tenant.IsAvailable && !string.IsNullOrEmpty(_tenant.Id) ? StorageScope.ForTenant(_tenant.Id) : StorageScope.Host;
        if (scope.Kind == StorageScopeKind.Host) await _files.RequirePermissionAsync("files.host", ct);
        var request = await _copies.FindCopyAsync(id, ct);
        if (request == null || request.OwnerId != _user.UserId || request.ScopeKey != scope.Key) throw new FileManagementException("not_found", "复制请求不存在。");
        return request;
    }
    public async Task<Guid> BindAsync(Guid id, CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var request = await GetAsync(id, ct);
            if (!await _authorizer.AuthorizeAsync(request.BusinessType, request.BusinessId, FileBusinessAccess.Bind, ct))
                throw new FileManagementException("forbidden", "当前已无目标业务关联权限。");
            var copy = await _files.AuthorizeReadAsync(request.CopyFileId, ct: ct);
            if (request.State == FileBindingCopyState.Bound) return copy.File.Id;
            if (request.State != FileBindingCopyState.Copied || copy.File.State != FileState.Ready) throw new FileManagementException("conflict", "副本尚不可绑定。");
            if (!copy.Bindings.Any(x => x.BusinessType == request.BusinessType && x.BusinessId == request.BusinessId))
                copy.Bindings.Add(new FileBinding { Id = Guid.NewGuid(), FileId = copy.File.Id, BusinessType = request.BusinessType,
                    BusinessId = request.BusinessId, CreatorId = _user.UserId, CreatedAt = FileManagementService.Now });
            request.State = FileBindingCopyState.Bound;
            if (await _copies.UpdateCopyAsync(request, request.Version, copy, copy.File.Version, ct: ct)) return copy.File.Id;
        }
        throw new FileManagementException("conflict", "副本绑定竞争。");
    }
    public async Task CancelAsync(Guid id, CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var request = await GetAsync(id, ct);
            if (request.State == FileBindingCopyState.Cancelled) return;
            var copy = await _store.FindAsync(request.CopyFileId, ct) ?? throw new FileManagementException("not_found", "副本不存在。");
            if (request.State == FileBindingCopyState.Bound || copy.Bindings.Count != 0) throw new FileManagementException("bound", "副本已绑定，不能取消。");
            request.State = FileBindingCopyState.Cancelled; copy.File.State = FileState.DeletePending;
            if (await _copies.UpdateCopyAsync(request, request.Version, copy, copy.File.Version,
                new[] { FileManagementService.Operation(copy.File.Id, FileOperationKind.Delete) }, ct: ct)) return;
        }
        throw new FileManagementException("conflict", "副本取消竞争。");
    }
    internal static async Task ExecuteAsync(IFileManagementStore store, IFileStorageFactory storage, FileManagementOptions options,
        FileOperation operation, FileOperationLease operationLease, CancellationToken ct)
    {
        var copies = store as IFileStorageTargetStore ?? throw new FileManagementException("configuration", "仓储未实现复制发布事务。");
        var request = await copies.FindCopyAsync(Guid.ParseExact(operation.Payload, "N"), ct) ?? throw new FileManagementException("not_found", "复制请求不存在。");
        if (request.State != FileBindingCopyState.Pending) return;
        var source = await store.FindAsync(request.SourceFileId, ct);
        var copy = await store.FindAsync(request.CopyFileId, ct) ?? throw new FileManagementException("not_found", "副本不存在。");
        if (source?.File.State != FileState.Ready || source.File.Version != request.SourceVersion || source.File.Ownership != RemoteDriveOwnership.Personal
            || source.File.CreatorId != request.OwnerId || source.File.ScopeKey != request.ScopeKey)
        {
            request.State = FileBindingCopyState.Failed; request.ErrorCode = "source_changed"; copy.File.State = FileState.Failed;
            if (!await copies.UpdateCopyAsync(request, request.Version, copy, copy.File.Version, operationLease: operationLease, ct: ct))
                throw new FileManagementException("conflict", "复制状态变化。");
            return;
        }
        var coordinator = new FileTransferCoordinator(store, storage, options, NullLogger.Instance);
        await coordinator.RunAsync(copy.File, copy.File.StoreName, copy.File.Container, async (attempt, transfer, token) =>
        {
            var input = storage.GetStorage(source.File.StoreName, source.File.Scope());
            var output = storage.GetStorage(copy.File.StoreName, copy.File.Scope());
            var address = new FileAddress(attempt.Container, attempt.ObjectKey); string? sourceHash = null;
            await input.ReadAsync(source.File.Address(), async (stream, readToken) =>
            {
                using var hash = new HashingReadStream(stream);
                await output.SaveAsync(new FileWriteRequest(address, copy.File.Length, FileWriteMode.Overwrite, copy.File.ContentType,
                    new Dictionary<string, string> { ["bing-session"] = copy.File.Id.ToString("N") }), hash, readToken);
                sourceHash = hash.Digest();
            }, token);
            await store.MarkTransferWrittenAsync(transfer, token);
            if ((await output.GetPropertiesAsync(address, token))?.Length != copy.File.Length || sourceHash != await FileMigrationService.HashAsync(output, address, token))
                throw new FileStorageException(FileStorageError.InvalidChecksum, "公共副本读回校验失败。");
            copy.File.ObjectKey = address.Key; copy.File.Sha256 = sourceHash;
            await new FileContentInspector(options).InspectAsync(copy.File, output, token);
            copy.File.State = FileState.Ready; request.State = FileBindingCopyState.Copied;
            if (!await copies.UpdateCopyAsync(request, request.Version, copy, copy.File.Version, operationLease: operationLease, transferLease: transfer,
                validateSource: true, ct: token)) throw new FileManagementException("conflict", "源文件、取消状态或复制租约已改变。");
        }, ct);
    }
}

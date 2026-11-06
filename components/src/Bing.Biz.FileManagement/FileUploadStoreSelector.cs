using Bing.FileStorage;
using Bing.FileStorage.RemoteDrive;

namespace Bing.Biz.FileManagement;

public sealed class FileUploadStoreSelector : IFileUploadStoreSelector
{
    private readonly IFileStorageFactory _factory;
    private readonly IFileStorageTargetStore? _store;
    private readonly IRemoteDriveStateStore? _connections;
    private readonly FileManagementOptions _options;
    public FileUploadStoreSelector(IFileStorageFactory factory, FileManagementOptions options, IFileStorageTargetStore? store = null, IRemoteDriveStateStore? connections = null)
    { _factory = factory; _store = store; _connections = connections; _options = options; }
    public async Task<FileUploadStoreSelection> SelectAsync(StorageScope scope, string ownerId, string container, string? targetId, bool businessCopy = false, CancellationToken ct = default)
    {
        if (!_options.Containers.TryGetValue(container, out var policy)) throw new FileManagementException("invalid", "未配置的容器。");
        var chosen = businessCopy ? policy.BusinessStorageTargetId : targetId;
        if (chosen == null && !businessCopy && _store != null) chosen = (await _store.FindPreferenceAsync(scope.Key, ownerId, container, ct))?.TargetId;
        if (chosen == null && _store != null) chosen = (await _store.FindPreferenceAsync(scope.Key, "", container, ct))?.TargetId;
        chosen ??= _factory.DefaultStore;
        if (Guid.TryParseExact(chosen, "N", out var id))
        {
            var connection = _connections == null ? null : await _connections.FindConnectionAsync(id, ct);
            if (connection == null || connection.ScopeKey != scope.Key || connection.State != RemoteDriveConnectionState.Active
                || connection.Ownership == RemoteDriveOwnership.Personal && (businessCopy || connection.OwnerId != ownerId))
                throw new FileManagementException("forbidden", "所选存储不可用或没有访问权限。");
            return new FileUploadStoreSelection { StoreName = connection.StoreName, ConnectionId = connection.Id, Ownership = connection.Ownership };
        }
        // 动态名称只允许通过连接 ID 选择，防止绕过连接所有权。
        var connectionMarker = chosen.LastIndexOf("-c-", StringComparison.Ordinal);
        if (connectionMarker >= 0 && Guid.TryParseExact(chosen[(connectionMarker + 3)..], "N", out _))
            throw new FileManagementException("invalid", "网盘上传目标必须使用连接标识。");
        _factory.GetStorage(chosen, scope);
        return new FileUploadStoreSelection { StoreName = chosen };
    }
}

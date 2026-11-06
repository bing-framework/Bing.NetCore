using System.Text.Json;

namespace Bing.FileStorage.RemoteDrive;

/// <summary>
/// 执行远程盘连接维护的调度入口。
/// </summary>
/// <remarks>
/// 由宿主既有调度器调用；失败退避持久化，避免坏连接长期占据队首。
/// </remarks>
public sealed class RemoteDriveMaintenance
{
    /// <summary>
    /// 保存远程盘维护使用的状态存储。
    /// </summary>
    private readonly IRemoteDriveStateStore _store;
    /// <summary>
    /// 保存用于清理远程盘文件的文件存储工厂。
    /// </summary>
    private readonly IFileStorageFactory _storage;
    /// <summary>
    /// 定义远程盘维护失败后的重试间隔。
    /// </summary>
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(5);
    /// <summary>
    /// 初始化一个 <see cref="RemoteDriveMaintenance" /> 类型的实例。
    /// </summary>
    /// <param name="store">用于读取或保存实例数据的存储实现。</param>
    /// <param name="storage">实例使用的存储实现。</param>
    public RemoteDriveMaintenance(IRemoteDriveStateStore store, IFileStorageFactory storage) { _store = store; _storage = storage; }
    /// <summary>
    /// 清理到期的远程分片传输记录。
    /// </summary>
    /// <param name="ct">用于取消清理和状态更新的令牌。</param>
    /// <returns>本次成功完成或中止的传输数量。</returns>
    /// <remarks>
    /// 每次最多处理 50 条记录；连接不可用或清理失败时推迟记录并保存固定错误类别，不持久化远程异常文本。
    /// </remarks>
    public async Task<int> CleanupAsync(CancellationToken ct = default)
    {
        var count = 0;
        foreach (var transfer in await _store.ListCleanupAsync(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), 50, ct))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var connection = await _store.FindConnectionAsync(transfer.ConnectionId, ct);
                if (connection == null || connection.State != RemoteDriveConnectionState.Active)
                {
                    await DeferAsync(transfer.Id, connection == null ? "connection_missing" : "connection_unavailable", ct);
                    continue;
                }
                var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(transfer.MetadataJson)
                    ?? throw new JsonException("无效中转元数据。");
                metadata["bing-upload-mode"] = "staged-proxy-v1";
                var upload = new MultipartUpload(transfer.Id.ToString("N"), FileWriteRequest.RestoreMultipart(new FileAddress(transfer.Container, transfer.ObjectKey),
                    transfer.Length, FileWriteMode.Overwrite, transfer.ContentType, metadata), transfer.PartSize);
                var storage = (IMultipartFileStorage)_storage.GetStorage(transfer.StoreName, connection.Scope());
                if (transfer.State == RemoteDriveTransferState.Verified)
                    await storage.CompleteMultipartAsync(upload, JsonSerializer.Deserialize<RemoteDrivePartReceipt[]>(transfer.ManifestJson)!.Select(x => x.ToPart()).ToArray(), ct);
                else await storage.AbortMultipartAsync(upload, ct);
                count++;
            }
            catch (Exception ex) when (ex is FileStorageException or IOException or System.Data.Common.DbException or JsonException or ArgumentException or InvalidOperationException)
            {
                // 只记录固定错误分类；不能把远端正文、签名 URL 或凭据写入数据库。
                await DeferAsync(transfer.Id, ex is FileStorageException storageError ? storageError.Error.ToString()
                    : ex is IOException ? "io" : ex is System.Data.Common.DbException ? "database" : "configuration", ct);
            }
        }
        return count;
    }
    /// <summary>
    /// 将清理失败的传输记录推迟到下一次重试。
    /// </summary>
    /// <param name="id">传输记录标识。</param>
    /// <param name="errorCode">要持久化的固定错误类别。</param>
    /// <param name="ct">用于取消状态查询和更新的令牌。</param>
    /// <remarks>
    /// 按当前记录版本设置下次尝试时间；记录不存在时不执行更新。
    /// </remarks>
    private async Task DeferAsync(Guid id, string errorCode, CancellationToken ct)
    {
        // Provider 可能已提交新版本或仍持有租约；不能用旧快照覆盖并发发布/清理。
        var current = await _store.FindTransferAsync(id, ct);
        if (current == null) return;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await _store.TryDeferCleanupAsync(id, current.Version, now, now + (long)RetryDelay.TotalMilliseconds, errorCode, ct);
    }
}

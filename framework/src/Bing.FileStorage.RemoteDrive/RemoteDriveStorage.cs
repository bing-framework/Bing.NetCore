using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.FileStorage.RemoteDrive;

/// <summary>
/// 持久中转分片并发布文件映射的远程盘存储。
/// </summary>
/// <remarks>
/// 浏览器分片持久中转，远端读回校验后原子发布映射。
/// </remarks>
internal sealed class RemoteDriveStorage : IFileStorage, IMultipartFileStorage, IMultipartFilePartReader, IFileStorageLimitProvider
{
    /// <summary>
    /// 保存远程盘存储关联的连接标识。
    /// </summary>
    private readonly Guid _connectionId;
    /// <summary>
    /// 保存远程盘存储使用的模板。
    /// </summary>
    private readonly RemoteDriveTemplate _template;
    /// <summary>
    /// 保存远程盘存储使用的服务提供程序。
    /// </summary>
    private readonly IServiceProvider _services;
    /// <summary>
    /// 初始化一个 <see cref="RemoteDriveStorage" /> 类型的实例。
    /// </summary>
    /// <param name="name">实例使用的名称。</param>
    /// <param name="scope">实例所属的作用范围。</param>
    /// <param name="connectionId">数据库连接标识。</param>
    /// <param name="template">用于配置远端存储连接或后端的模板。</param>
    /// <param name="services">用于注册或解析实例依赖项的服务集合。</param>
    public RemoteDriveStorage(string name, StorageScope scope, Guid connectionId, RemoteDriveTemplate template, IServiceProvider services)
    { Name = name; Scope = scope; _connectionId = connectionId; _template = template; _services = services; }
    /// <inheritdoc />
    public string Name { get; }
    /// <inheritdoc />
    public StorageScope Scope { get; }
    /// <inheritdoc />
    public FileStorageCapabilities Capabilities => FileStorageCapabilities.Multipart | FileStorageCapabilities.ServerVerifiedParts | FileStorageCapabilities.RequiresCompletionCleanup;
    /// <summary>
    /// 获取当前 UTC 时间的 Unix 毫秒值。
    /// </summary>
    /// <remarks>数值以毫秒为单位，自 Unix 纪元起计算。</remarks>
    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    /// <summary>
    /// 将 JSON 文本反序列化为指定类型的对象。
    /// </summary>
    /// <param name="json">待反序列化的 JSON 文本。</param>
    /// <typeparam name="T">反序列化结果的类型。</typeparam>
    /// <returns>反序列化得到的对象。</returns>
    private static T Decode<T>(string json) => JsonSerializer.Deserialize<T>(json) ?? throw new FileStorageException(FileStorageError.Configuration, "无效持久化检查点。");
    /// <summary>
    /// 创建表示状态冲突的文件存储异常。
    /// </summary>
    /// <returns>表示远端传输状态与当前请求不一致的文件存储冲突异常。</returns>
    private static FileStorageException Conflict() => new(FileStorageError.Conflict, "网盘传输状态已变化，请重新查询。");
    /// <summary>
    /// 创建依赖注入作用域并使用远程存储状态仓储执行操作。
    /// </summary>
    /// <param name="action">接收状态仓储并执行的异步操作。</param>
    /// <typeparam name="T">异步操作结果的类型。</typeparam>
    /// <returns>操作返回的结果。</returns>
    private async Task<T> Db<T>(Func<IRemoteDriveStateStore, Task<T>> action)
    {
        await using var scope = _services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IRemoteDriveStateStore>());
    }
    /// <summary>
    /// 获取并验证当前远程存储连接上下文。
    /// </summary>
    /// <param name="ct">用于取消连接及凭据查询的令牌。</param>
    /// <returns>通过验证的远程存储连接上下文。</returns>
    /// <remarks>
    /// 连接需要重新授权时会尝试刷新凭据并重新读取连接；连接不存在、归属不符或未处于活动状态时会抛出访问拒绝错误。
    /// </remarks>
    private async Task<RemoteDriveContext> Context(CancellationToken ct)
    {
        var c = await Db(s => s.FindConnectionAsync(_connectionId, ct));
        if (c?.State == RemoteDriveConnectionState.ReauthorizationRequired && c.LeaseUntil > Now && c.ScopeKey == Scope.Key)
        {
            // 其他进程正在轮换令牌时等待其 CAS 结果，不能把短暂刷新状态当成永久撤销。
            await using var refreshScope = _services.CreateAsyncScope();
            await refreshScope.ServiceProvider.GetRequiredService<IRemoteDriveCredentialProvider>().GetAsync(c.Id, ct: ct);
            c = await Db(s => s.FindConnectionAsync(_connectionId, ct));
        }
        if (c == null || c.StoreName != Name || c.TemplateName != _template.Name || c.ScopeKey != Scope.Key
            || c.State != RemoteDriveConnectionState.Active)
            throw new FileStorageException(FileStorageError.AccessDenied, "网盘连接不可用或不属于当前范围。");
        return new RemoteDriveContext(c, async (refresh, token) =>
        {
            await using var scope = _services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IRemoteDriveCredentialProvider>().GetAsync(c.Id, refresh, token);
        });
    }
    /// <inheritdoc />
    public async Task<FileStorageLimits?> GetLimitsAsync(CancellationToken cancellationToken = default)
    { await Context(cancellationToken); return new FileStorageLimits { MaximumFileLength = _template.MaximumFileLength, PrivateContainersOnly = true }; }
    /// <summary>
    /// 获取并验证传输文件所用的临时存储。
    /// </summary>
    /// <param name="t">包含临时存储信息的传输记录。</param>
    /// <returns>支持分片和范围读取的临时文件存储。</returns>
    /// <remarks>
    /// 临时存储不支持分片或范围读取时会抛出配置错误。
    /// </remarks>
    private IFileStorage Stage(RemoteDriveTransfer t)
    {
        var stage = _services.GetRequiredService<IFileStorageFactory>().GetStorage(t.StageStoreName, Scope);
        if (!stage.Capabilities.HasFlag(FileStorageCapabilities.Multipart)
            || !stage.Capabilities.HasFlag(FileStorageCapabilities.Range))
            throw new FileStorageException(FileStorageError.Configuration, "网盘中转必须使用支持分片与 Range 的公共存储。");
        return stage;
    }
    /// <summary>
    /// 根据传输记录创建临时文件地址。
    /// </summary>
    /// <param name="t">包含临时存储容器和对象键的传输记录。</param>
    /// <returns>传输记录对应的临时文件地址。</returns>
    private static FileAddress StageAddress(RemoteDriveTransfer t) => new(t.StageContainer, t.StageObjectKey);
    /// <summary>
    /// 根据传输记录还原临时文件写入请求。
    /// </summary>
    /// <param name="t">包含临时文件写入信息的传输记录。</param>
    /// <returns>恢复出的分片文件写入请求。</returns>
    private static FileWriteRequest StageRequest(RemoteDriveTransfer t) => FileWriteRequest.RestoreMultipart(StageAddress(t), t.Length,
        FileWriteMode.Overwrite, t.ContentType, Decode<Dictionary<string, string>>(t.StageMetadataJson));
    /// <summary>
    /// 根据传输记录还原临时分片上传会话。
    /// </summary>
    /// <param name="t">包含临时上传标识和分片大小的传输记录。</param>
    /// <returns>恢复出的临时分片上传会话。</returns>
    /// <remarks>
    /// 传输记录没有临时上传标识时会报告状态冲突。
    /// </remarks>
    private static MultipartUpload StageUpload(RemoteDriveTransfer t) => new(t.StageUploadId ?? throw Conflict(), StageRequest(t), t.PartSize);
    /// <summary>
    /// 根据传输记录还原目标存储的分片上传会话。
    /// </summary>
    /// <param name="t">包含目标对象和写入元数据的传输记录。</param>
    /// <returns>恢复出的目标分片上传会话。</returns>
    private MultipartUpload Upload(RemoteDriveTransfer t)
    {
        var metadata = Decode<Dictionary<string, string>>(t.MetadataJson);
        metadata["bing-upload-mode"] = "staged-proxy-v1";
        return new MultipartUpload(t.Id.ToString("N"), FileWriteRequest.RestoreMultipart(new FileAddress(t.Container, t.ObjectKey),
            t.Length, FileWriteMode.Overwrite, t.ContentType, metadata), t.PartSize);
    }
    /// <summary>
    /// 根据上传会话查找并验证持久化的传输记录。
    /// </summary>
    /// <param name="upload">要验证的目标分片上传会话。</param>
    /// <param name="ct">用于取消数据库查询的令牌。</param>
    /// <returns>与上传会话及当前连接、存储和作用域匹配的传输记录。</returns>
    /// <remarks>
    /// 上传标识无法解析、记录不存在或任一目标信息不匹配时会报告状态冲突。
    /// </remarks>
    private async Task<RemoteDriveTransfer> Transfer(MultipartUpload upload, CancellationToken ct)
    {
        if (!Guid.TryParseExact(upload.UploadId, "N", out var id)) throw Conflict();
        var t = await Db(s => s.FindTransferAsync(id, ct));
        if (t == null || t.ConnectionId != _connectionId || t.ScopeKey != Scope.Key || t.StoreName != Name
            || t.Container != upload.Request.Address.Container || t.ObjectKey != upload.Request.Address.Key
            || t.Length != upload.Request.Length || t.PartSize != upload.PartSize
            || !upload.Request.Metadata.TryGetValue("bing-upload-mode", out var mode) || mode != "staged-proxy-v1") throw Conflict();
        return t;
    }
    /// <summary>
    /// 使用当前版本和租约更新传输记录。
    /// </summary>
    /// <param name="t">要保存的传输记录。</param>
    /// <param name="lease">用于验证当前更新权限的租约标识；没有租约时为 null。</param>
    /// <param name="ct">用于取消数据库更新的令牌。</param>
    /// <remarks>
    /// 并发版本或租约校验失败时会报告状态冲突。
    /// </remarks>
    private async Task SaveTransfer(RemoteDriveTransfer t, string? lease, CancellationToken ct)
    { if (!await Db(s => s.TryUpdateTransferAsync(t, t.Version, lease, ct))) throw Conflict(); }
    /// <summary>
    /// 将清理异常转换为持久化错误代码。
    /// </summary>
    /// <param name="error">导致清理失败的异常。</param>
    /// <returns>文件存储错误名称、io、timeout、database 或 unknown。</returns>
    private static string CleanupErrorCode(Exception error)
    {
        if (error is FileStorageException storage)
            return storage.Error.ToString();
        if (error is IOException) return "io";
        if (error is TimeoutException) return "timeout";
        if (error is System.Data.Common.DbException) return "database";
        return "unknown";
    }
    /// <summary>
    /// 清理失败后释放有效租约并记录下一次重试信息。
    /// </summary>
    /// <param name="transferId">传输记录标识。</param>
    /// <param name="lease">当前清理租约标识。</param>
    /// <param name="error">导致清理失败的异常。</param>
    /// <remarks>
    /// 仅当传输仍持有未过期租约时更新重试时间、错误代码并释放租约；记录更新失败时保留原清理异常。
    /// </remarks>
    private async Task ReleaseCleanupLeaseAfterFailureAsync(Guid transferId, string lease, Exception error)
    {
        // 使用独立的短超时保存退避状态，不能因为调用方取消而遗留一个长时间占用的租约。
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            var current = await Db(s => s.FindTransferAsync(transferId, cleanup.Token));
            var now = Now;
            if (current == null || current.LeaseToken != lease || current.LeaseUntil <= now) return;
            current.CleanupCompleted = false;
            current.CleanupNextAttemptAt = now + (long)TimeSpan.FromMinutes(5).TotalMilliseconds;
            current.CleanupErrorCode = CleanupErrorCode(error);
            current.LeaseToken = null;
            current.LeaseUntil = 0;
            await Db(s => s.TryUpdateTransferAsync(current, current.Version, lease, cleanup.Token));
        }
        catch
        {
            // 保存失败时保留原异常；过期租约仍可由后续维护轮次接管。
        }
    }

    /// <inheritdoc />
    public async Task<MultipartUpload> CreateMultipartAsync(FileWriteRequest request, int partSize, CancellationToken cancellationToken = default)
    {
        await Context(cancellationToken);
        if (request.Mode == FileWriteMode.CreateOnly) throw new FileStorageException(FileStorageError.UnsupportedCapability, "网盘不提供原子 CreateOnly。");
        if (request.Metadata.Keys.Any(FileWriteRequest.IsMultipartMetadata)) throw new ArgumentException("保留元数据不能由调用方指定。");
        if (request.Length > _template.MaximumFileLength) throw new FileStorageException(FileStorageError.CapacityExceeded, "文件超过账号确认上限。");
        var id = Guid.NewGuid();
        _ = new MultipartUpload(id.ToString("N"), request, partSize);
        var previous = await Db(s => s.FindObjectAsync(RemoteDriveObject.Key(Name, Scope.Key, request.Address), cancellationToken));
        var t = new RemoteDriveTransfer { Id = id, ConnectionId = _connectionId, StoreName = Name, ScopeKey = Scope.Key,
            Container = request.Address.Container, ObjectKey = request.Address.Key, Length = request.Length, PartSize = partSize,
            ContentType = request.ContentType, MetadataJson = JsonSerializer.Serialize(request.Metadata),
            StageStoreName = _template.StagingStoreName, StageContainer = _template.StagingContainer, StageObjectKey = "drive-stage/" + id.ToString("N"),
            StageMetadataJson = JsonSerializer.Serialize(new Dictionary<string, string> { ["bing-session"] = id.ToString("N") }),
            PreviousRemoteFileJson = previous?.Deleted == false ? previous.RemoteFileJson : null, ExpectedObjectVersion = previous?.Version ?? 0,
            ExpiresAt = Now + (long)_template.TransferLifetime.TotalMilliseconds, CreatedAt = Now };
        var stage = Stage(t);
        if (!await Db(s => s.TryCreateTransferAsync(t, cancellationToken))) throw Conflict();
        try
        {
            if (t.Length > 0)
            {
                var upload = await ((IMultipartFileStorage)stage).CreateMultipartAsync(StageRequest(t), partSize, cancellationToken);
                t.StageUploadId = upload.UploadId; t.StageMetadataJson = JsonSerializer.Serialize(upload.Request.Metadata);
            }
            t.State = RemoteDriveTransferState.Receiving;
            await SaveTransfer(t, null, cancellationToken);
            return Upload(t);
        }
        catch
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                // 后端句柄已返回但落库失败时，先保留句柄再回收，失败仍有可执行记录。
                var current = await Db(s => s.FindTransferAsync(t.Id, cleanup.Token));
                if (current != null && current.State <= RemoteDriveTransferState.Receiving)
                {
                    current.StageUploadId = t.StageUploadId; current.StageMetadataJson = t.StageMetadataJson;
                    current.State = RemoteDriveTransferState.Aborted; current.ExpiresAt = Now;
                    await SaveTransfer(current, null, cleanup.Token);
                    if (current.StageUploadId != null) await ((IMultipartFileStorage)stage).AbortMultipartAsync(StageUpload(current), cleanup.Token);
                    await stage.DeleteAsync(StageAddress(current), cleanup.Token);
                    current.CleanupCompleted = current.StageUploadId != null || current.Length == 0;
                    await SaveTransfer(current, null, cleanup.Token);
                }
            }
            catch { /* 元数据或中转不可用时保留初始化意图，依靠期限与后端生命周期清理未知句柄。 */ }
            throw;
        }
    }
    /// <inheritdoc />
    public async Task<FilePart> UploadPartAsync(MultipartUpload upload, int partNumber, Stream source, string? sha256 = null, CancellationToken cancellationToken = default)
    {
        await Context(cancellationToken);
        var t = await Transfer(upload, cancellationToken);
        if (t.State != RemoteDriveTransferState.Receiving || t.ExpiresAt <= Now) throw Conflict();
        var receipt = await ((IMultipartFileStorage)Stage(t)).UploadPartAsync(StageUpload(t), partNumber, source, sha256, cancellationToken);
        var current = await Transfer(upload, cancellationToken);
        if (current.State != RemoteDriveTransferState.Receiving || current.ExpiresAt <= Now) throw Conflict();
        return receipt;
    }
    /// <inheritdoc />
    public async Task<IReadOnlyList<FilePart>> ListPartsAsync(MultipartUpload upload, CancellationToken cancellationToken = default)
    {
        await Context(cancellationToken); var t = await Transfer(upload, cancellationToken);
        if (t.State == RemoteDriveTransferState.Aborted) throw new FileStorageException(FileStorageError.UploadNotFound, "上传已中止。");
        if (t.State >= RemoteDriveTransferState.Frozen) return Decode<RemoteDrivePartReceipt[]>(t.ManifestJson).Select(x => x.ToPart()).ToArray();
        if (t.Length == 0) return Array.Empty<FilePart>();
        return await ((IMultipartFileStorage)Stage(t)).ListPartsAsync(StageUpload(t), cancellationToken);
    }
    /// <inheritdoc />
    public async Task<FilePart?> GetPartAsync(MultipartUpload upload, int partNumber, CancellationToken cancellationToken = default)
    {
        upload.GetPartLength(partNumber); await Context(cancellationToken); var t = await Transfer(upload, cancellationToken);
        if (t.State >= RemoteDriveTransferState.Frozen) return (await ListPartsAsync(upload, cancellationToken)).SingleOrDefault(x => x.Number == partNumber);
        return await ((IMultipartFilePartReader)Stage(t)).GetPartAsync(StageUpload(t), partNumber, cancellationToken);
    }
    /// <inheritdoc />
    public async Task<FileWriteResult> SaveAsync(FileWriteRequest request, Stream source, CancellationToken cancellationToken = default)
    {
        FileStorageStreams.ValidateSource(source, request.Length);
        var upload = await CreateMultipartAsync(request, 16 * 1024 * 1024, cancellationToken);
        try
        {
            var parts = new List<FilePart>();
            for (var number = 1; number <= upload.PartCount; number++)
            {
                using var part = new BoundedReadStream(source, upload.GetPartLength(number));
                parts.Add(await UploadPartAsync(upload, number, part, null, cancellationToken));
            }
            if (await source.ReadAsync(new byte[1], 0, 1, cancellationToken) != 0)
                throw new FileStorageException(FileStorageError.InvalidLength, "输入流超过声明长度。");
            return await CompleteMultipartAsync(upload, parts, cancellationToken);
        }
        catch
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try { await AbortMultipartAsync(upload, cleanup.Token); }
            catch { /* 中转和远端地址已经持久登记，由维护任务继续回收。 */ }
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<FileWriteResult> CompleteMultipartAsync(MultipartUpload upload, IReadOnlyList<FilePart> parts, CancellationToken cancellationToken = default)
    {
        FileStorageStreams.ValidateParts(upload, parts);
        var context = await Context(cancellationToken); var t = await Transfer(upload, cancellationToken);
        if (t.State == RemoteDriveTransferState.Aborted || t.State < RemoteDriveTransferState.Frozen && t.ExpiresAt <= Now) throw Conflict();
        var objectId = RemoteDriveObject.Key(Name, Scope.Key, upload.Request.Address);
        if (t.State == RemoteDriveTransferState.Completed)
        {
            var done = await Db(s => s.FindObjectAsync(objectId, cancellationToken));
            if (done == null || done.Deleted || done.TransferId != t.Id) throw Conflict();
            return Result(upload.Request.Address, done);
        }
        var lease = Guid.NewGuid().ToString("N");
        if (!await Db(s => s.TryClaimTransferAsync(t.Id, t.Version, lease, Now + (long)_template.LeaseDuration.TotalMilliseconds, cancellationToken))) throw Conflict();
        t = await Transfer(upload, cancellationToken);
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var heartbeat = Heartbeat(t.Id, lease, lifetime);
        try
        {
            var ct = lifetime.Token;
            if (t.State == RemoteDriveTransferState.Receiving)
            {
                var remoteParts = await ListPartsAsync(upload, ct);
                if (remoteParts.Count != parts.Count || parts.Where((p, i) => p.Number != remoteParts[i].Number || p.Length != remoteParts[i].Length
                    || p.ETag != remoteParts[i].ETag || string.IsNullOrEmpty(p.Sha256)
                    || remoteParts[i].Sha256 != null && p.Sha256 != remoteParts[i].Sha256).Any())
                    throw new FileStorageException(FileStorageError.InvalidPart, "中转分片已变化，必须重新上传或确认。");
                t.ManifestJson = JsonSerializer.Serialize(parts.Select(RemoteDrivePartReceipt.From)); t.State = RemoteDriveTransferState.Frozen;
                await SaveTransfer(t, lease, ct);
            }
            var stage = Stage(t); var address = StageAddress(t);
            RemoteDriveObject? published = await Db(s => s.FindObjectAsync(objectId, ct));
            if (t.State != RemoteDriveTransferState.Verified)
            {
                var stageProperties = await stage.GetPropertiesAsync(address, ct);
                if (stageProperties == null)
                {
                    if (t.Length == 0) await stage.SaveAsync(StageRequest(t), Stream.Null, ct);
                    else await ((IMultipartFileStorage)stage).CompleteMultipartAsync(StageUpload(t), Decode<RemoteDrivePartReceipt[]>(t.ManifestJson).Select(x => x.ToPart()).ToArray(), ct);
                    stageProperties = await stage.GetPropertiesAsync(address, ct);
                }
                if (stageProperties?.Length != t.Length || !stageProperties.Metadata.TryGetValue("bing-session", out var session) || session != t.Id.ToString("N"))
                    throw new FileStorageException(FileStorageError.InvalidChecksum, "持久中转对象不匹配。");
                t.StageCompleted = true; t.State = RemoteDriveTransferState.Uploading; await SaveTransfer(t, lease, ct);
                var content = new RemoteDriveContent(t.Length, (offset, length, consume, token) => length == 0
                    ? consume(Stream.Null, token) : ((IFileRangeReader)stage).ReadRangeAsync(address, offset, length, consume, token));
                var checkpoint = Decode<RemoteDriveCheckpoint>(t.CheckpointJson);
                checkpoint.Name = "bing-" + t.Id.ToString("N");
                if (checkpoint.Sha256 == null)
                {
                    await content.ReadAsync(async (stream, token) => checkpoint.Sha256 = await FileStorageStreams.CopyExactAsync(stream, Stream.Null, t.Length, token), ct);
                    t.CheckpointJson = JsonSerializer.Serialize(checkpoint); await SaveTransfer(t, lease, ct);
                }
                var remote = await _template.Client.UploadAsync(context, content, checkpoint, async (saved, token) =>
                { t.CheckpointJson = JsonSerializer.Serialize(saved); await SaveTransfer(t, lease, token); }, ct);
                if (remote.Length != t.Length) throw new FileStorageException(FileStorageError.InvalidLength, "网盘完成后的长度不匹配。");
                var download = await _template.Client.DownloadAsync(context, remote, ct);
                string? hash = null;
                await _services.GetRequiredService<RemoteDriveTransport>().ReadAsync(download, async (stream, token) =>
                    hash = await FileStorageStreams.CopyExactAsync(stream, Stream.Null, t.Length, token), ct, _template.TransportTimeouts);
                if (hash != checkpoint.Sha256) throw new FileStorageException(FileStorageError.InvalidChecksum, "网盘读回摘要不匹配。");
                checkpoint.CompletedFile = remote; t.CheckpointJson = JsonSerializer.Serialize(checkpoint); t.State = RemoteDriveTransferState.Verified;
                published = new RemoteDriveObject { Id = objectId, ConnectionId = _connectionId, TransferId = t.Id, StoreName = Name, ScopeKey = Scope.Key,
                    Container = t.Container, ObjectKey = t.ObjectKey, RemoteFileJson = JsonSerializer.Serialize(remote), MetadataJson = t.MetadataJson,
                    ContentType = t.ContentType, Length = t.Length, Sha256 = hash! };
                if (!await Db(s => s.TryPublishAsync(published, t.ExpectedObjectVersion, t, t.Version, lease, ct))) throw Conflict();
            }
            if (published == null || published.Deleted || published.TransferId != t.Id) throw Conflict();
            await stage.DeleteAsync(address, ct);
            if (t.StageUploadId != null) await ((IMultipartFileStorage)stage).AbortMultipartAsync(StageUpload(t), ct);
            if (t.PreviousRemoteFileJson != null)
                await _template.Client.DeleteAsync(context, Decode<RemoteDriveFile>(t.PreviousRemoteFileJson), ct);
            t.State = RemoteDriveTransferState.Completed; t.CleanupCompleted = true; t.CleanupNextAttemptAt = 0; t.CleanupErrorCode = null; await SaveTransfer(t, lease, ct);
            return Result(upload.Request.Address, published);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && lifetime.IsCancellationRequested)
        {
            // 心跳停止属于可恢复的租约/存储故障，不能作为普通取消进入永久失败及中止流程。
            throw await heartbeat ?? Conflict();
        }
        finally
        {
            lifetime.Cancel();
            await heartbeat;
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                var current = await Db(s => s.FindTransferAsync(t.Id, cleanup.Token));
                if (current?.LeaseToken == lease)
                {
                    current.LeaseToken = null; current.LeaseUntil = 0;
                    await Db(s => s.TryUpdateTransferAsync(current, current.Version, lease, cleanup.Token));
                }
            }
            catch { /* 到期租约仍可被后继执行者接管。 */ }
        }
    }
    /// <summary>
    /// 续租远程传输记录并在续租失败时结束传输。
    /// </summary>
    /// <param name="id">传输记录标识。</param>
    /// <param name="lease">当前传输租约标识。</param>
    /// <param name="lifetime">控制传输取消和续租任务的令牌源。</param>
    /// <returns>检测到租约冲突或续租失败时返回相应文件存储异常；传输取消且未发生续租错误时返回 null。</returns>
    private async Task<FileStorageException?> Heartbeat(Guid id, string lease, CancellationTokenSource lifetime)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_template.LeaseDuration.TotalMilliseconds / 3));
        try
        {
            while (await timer.WaitForNextTickAsync(lifetime.Token))
                if (!await Db(s => s.RenewTransferAsync(id, lease, Now + (long)_template.LeaseDuration.TotalMilliseconds, lifetime.Token)))
                { lifetime.Cancel(); return Conflict(); }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception exception)
        {
            // 在上传边界传播稳定错误，finally 等待心跳时不再覆盖调用方取消或原始失败。
            lifetime.Cancel();
            return new FileStorageException(FileStorageError.Unavailable, "网盘传输续租暂时不可用。", exception);
        }
        return null;
    }
    /// <summary>
    /// 根据文件地址和远程对象创建文件写入结果。
    /// </summary>
    /// <param name="address">目标文件地址。</param>
    /// <param name="obj">对应的远程存储对象记录。</param>
    /// <returns>包含文件位置和属性的写入结果。</returns>
    private FileWriteResult Result(FileAddress address, RemoteDriveObject obj) => new(this.Location(address), Properties(obj));
    /// <summary>
    /// 将远程存储对象记录转换为文件属性。
    /// </summary>
    /// <param name="obj">包含远程文件及其元数据的存储对象记录。</param>
    /// <returns>由对象记录生成的文件属性。</returns>
    private static FileProperties Properties(RemoteDriveObject obj)
    {
        var file = Decode<RemoteDriveFile>(obj.RemoteFileJson);
        return new FileProperties { Length = obj.Length, ContentType = obj.ContentType, ETag = obj.Sha256, VersionId = obj.Version.ToString(),
            LastModifiedUtc = file.ModifiedAt, Metadata = Decode<Dictionary<string, string>>(obj.MetadataJson) };
    }
    /// <summary>
    /// 查找并验证远程存储连接及目标文件。
    /// </summary>
    /// <param name="address">要查找的文件地址。</param>
    /// <param name="ct">用于取消连接、数据库和远程文件查询的令牌。</param>
    /// <returns>连接上下文、存储对象记录和远程文件；对象不存在或已删除，或远程文件不存在时，后两项为 null。</returns>
    /// <remarks>
    /// 远程文件的长度、内容哈希或版本与存储记录不符时会报告状态冲突。
    /// </remarks>
    private async Task<(RemoteDriveContext Context, RemoteDriveObject? Object, RemoteDriveFile? File)> Find(FileAddress address, CancellationToken ct)
    {
        var context = await Context(ct);
        var obj = await Db(s => s.FindObjectAsync(RemoteDriveObject.Key(Name, Scope.Key, address), ct));
        if (obj == null || obj.Deleted) return (context, null, null);
        var saved = Decode<RemoteDriveFile>(obj.RemoteFileJson);
        var actual = await _template.Client.GetFileAsync(context, saved.Id, saved.ParentId, ct);
        if (actual == null) return (context, null, null);
        if (actual.Length != obj.Length || actual.ContentHash != saved.ContentHash || actual.Version != saved.Version)
            throw new FileStorageException(FileStorageError.Conflict, "远端文件已在应用外变化。");
        return (context, obj, actual);
    }
    /// <inheritdoc />
    public async Task<FileProperties?> GetPropertiesAsync(FileAddress address, CancellationToken cancellationToken = default)
    { var found = await Find(address, cancellationToken); return found.Object == null ? null : Properties(found.Object); }
    /// <inheritdoc />
    public async Task ReadAsync(FileAddress address, Func<Stream, CancellationToken, Task> consume, CancellationToken cancellationToken = default)
    {
        var found = await Find(address, cancellationToken);
        if (found.File == null) throw new FileStorageException(FileStorageError.NotFound, "网盘对象不存在。");
        var download = await _template.Client.DownloadAsync(found.Context, found.File, cancellationToken);
        await _services.GetRequiredService<RemoteDriveTransport>().ReadAsync(download, consume, cancellationToken, _template.TransportTimeouts);
    }
    /// <inheritdoc />
    public async Task DeleteAsync(FileAddress address, CancellationToken cancellationToken = default)
    {
        var context = await Context(cancellationToken);
        var obj = await Db(s => s.FindObjectAsync(RemoteDriveObject.Key(Name, Scope.Key, address), cancellationToken));
        if (obj == null) return;
        if (!obj.Deleted && !await Db(s => s.TryDeleteObjectAsync(obj.Id, obj.Version, cancellationToken))) throw Conflict();
        await _template.Client.DeleteAsync(context, Decode<RemoteDriveFile>(obj.RemoteFileJson), cancellationToken);
    }
    /// <inheritdoc />
    public async Task AbortMultipartAsync(MultipartUpload upload, CancellationToken cancellationToken = default)
    {
        var context = await Context(cancellationToken); var t = await Transfer(upload, cancellationToken);
        if (t.State is RemoteDriveTransferState.Completed or RemoteDriveTransferState.Verified) return;
        var lease = Guid.NewGuid().ToString("N");
        if (!await Db(s => s.TryClaimTransferAsync(t.Id, t.Version, lease, Now + (long)_template.LeaseDuration.TotalMilliseconds, cancellationToken))) throw Conflict();
        try
        {
            t = await Transfer(upload, cancellationToken); t.State = RemoteDriveTransferState.Aborted; await SaveTransfer(t, lease, cancellationToken);
            await _template.Client.AbortAsync(context, Decode<RemoteDriveCheckpoint>(t.CheckpointJson), cancellationToken);
            var stage = Stage(t);
            if (t.StageUploadId != null) await ((IMultipartFileStorage)stage).AbortMultipartAsync(StageUpload(t), cancellationToken);
            await stage.DeleteAsync(StageAddress(t), cancellationToken);
            if (t.Length > 0 && t.StageUploadId == null)
                throw new FileStorageException(FileStorageError.OutcomeUnknown, "中转初始化结果未知，保留意图等待后端生命周期对账。");
            t.CleanupCompleted = true; t.CleanupNextAttemptAt = 0; t.CleanupErrorCode = null; t.LeaseToken = null; t.LeaseUntil = 0; await SaveTransfer(t, lease, cancellationToken);
        }
        catch (OperationCanceledException ex)
        {
            await ReleaseCleanupLeaseAfterFailureAsync(t.Id, lease, ex);
            throw;
        }
        catch (Exception ex)
        {
            await ReleaseCleanupLeaseAfterFailureAsync(t.Id, lease, ex);
            if (ex is FileStorageException) throw;
            throw new FileStorageException(FileStorageError.OutcomeUnknown, "网盘清理结果未知，已安排重试。", ex);
        }
    }
}

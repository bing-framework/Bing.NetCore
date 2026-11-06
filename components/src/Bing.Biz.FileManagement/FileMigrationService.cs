using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bing.FileStorage;
using Bing.FileStorage.RemoteDrive;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bing.Biz.FileManagement;

/// <summary>只由已授权管理工具调用。复制、校验和元数据切换分开；所有位置变更使用文件版本。</summary>
public sealed class FileMigrationService
{
    private readonly IFileManagementStore _store;
    private readonly IFileStorageFactory _storage;
    private readonly FileManagementOptions _options;
    private readonly FileTransferCoordinator _transfers;
    public FileMigrationService(IFileManagementStore store, IFileStorageFactory storage, FileManagementOptions options, ILogger<FileMigrationService>? logger = null)
    { _store = store; _storage = storage; _options = options; _transfers = new(store, storage, options, logger ?? NullLogger<FileMigrationService>.Instance); }
    internal Task CleanupTransfersAsync(CancellationToken ct) => _transfers.CleanupAsync(ct);
    public sealed class Replica
    {
        public string StoreName { get; set; } = "";
        public string Container { get; set; } = "";
        public string ObjectKey { get; set; } = "";
        public long ExpectedVersion { get; set; }
    }
    public async Task<Guid> QueueMigrationAsync(Guid fileId, string targetStore, CancellationToken ct = default)
    {
        var entry = await _store.FindAsync(fileId, ct) ?? throw new FileManagementException("not_found", "文件不存在。");
        if (entry.File.Ownership == RemoteDriveOwnership.Personal) throw new FileManagementException("forbidden", "个人文件只能由本人通过复制发布流程转移。");
        await ValidatePublicTargetAsync(_store, targetStore, entry.File.ScopeKey, ct);
        if (entry.File.State != FileState.Ready) throw new FileManagementException("conflict", "只能迁移 Ready 文件。");
        var backend = _storage.GetStorage(targetStore, entry.File.Scope());
        if (entry.File.StoreName == targetStore) throw new FileManagementException("invalid", "来源与目标相同。");
        await ValidateTargetLimitsAsync(entry.File, backend, ct);
        var operation = FileManagementService.Operation(fileId, FileOperationKind.Migrate, JsonSerializer.Serialize(new Replica
        { StoreName = targetStore, Container = entry.File.Container, ExpectedVersion = entry.File.Version + 1 }));
        if (!await _store.TryUpdateAsync(entry, entry.File.Version, operations: new[] { operation }, ct: ct))
            throw new FileManagementException("conflict", "迁移排队竞争，请重试。");
        return operation.Id;
    }
    internal async Task ExecuteMigrationAsync(FileOperation operation, FileOperationLease operationLease, CancellationToken ct)
    {
        var target = JsonSerializer.Deserialize<Replica>(operation.Payload) ?? throw new InvalidOperationException("迁移参数损坏。");
        var entry = await _store.FindAsync(operation.FileId, ct) ?? throw new FileManagementException("not_found", "文件不存在。");
        if (entry.File.State != FileState.Ready || entry.File.Version != target.ExpectedVersion) return;
        if (entry.File.Ownership == RemoteDriveOwnership.Personal) throw new FileManagementException("forbidden", "不能通过管理迁移读取个人文件。");
        var targetConnection = await ValidatePublicTargetAsync(_store, target.StoreName, entry.File.ScopeKey, ct);
        var source = _storage.GetStorage(entry.File.StoreName, entry.File.Scope());
        var backend = _storage.GetStorage(target.StoreName, entry.File.Scope());
        // 任务可能在排队后才执行；复制前重新检查，亦覆盖批量迁移和恢复任务。
        await ValidateTargetLimitsAsync(entry.File, backend, ct);
        await _transfers.RunAsync(entry.File, target.StoreName, target.Container, async (attempt, transfer, token) =>
        {
            var address = new FileAddress(attempt.Container, attempt.ObjectKey);
            string? sourceHash = null;
            await source.ReadAsync(entry.File.Address(), async (input, readToken) =>
            {
                using var hashing = new HashingReadStream(input);
                await backend.SaveAsync(new FileWriteRequest(address, entry.File.Length, FileWriteMode.Overwrite, entry.File.ContentType,
                    new Dictionary<string, string> { ["bing-session"] = (entry.Session?.Id ?? entry.File.Id).ToString("N") }), hashing, readToken);
                sourceHash = hashing.Digest();
            }, token);
            await _store.MarkTransferWrittenAsync(transfer, token);
            var properties = await backend.GetPropertiesAsync(address, token);
            var targetHash = await HashAsync(backend, address, token);
            if (properties?.Length != entry.File.Length || sourceHash != targetHash)
                throw new FileStorageException(FileStorageError.InvalidChecksum, "迁移复制校验失败。");
            var old = new Replica { StoreName = entry.File.StoreName, Container = entry.File.Container, ObjectKey = entry.File.ObjectKey };
            entry.File.StoreName = attempt.StoreName; entry.File.ConnectionId = targetConnection;
            entry.File.Container = attempt.Container; entry.File.ObjectKey = attempt.ObjectKey; entry.File.Sha256 = targetHash;
            var cleanup = FileManagementService.Operation(entry.File.Id, FileOperationKind.DeleteReplica, JsonSerializer.Serialize(old),
                FileManagementService.Now + (long)_options.ReplicaRetention.TotalMilliseconds);
            // 租约、目标发布与业务位置在同一事务提交；失去租约的副本只能退休，不能改写胜者目标。
            if (!await _store.TryUpdateAsync(entry, target.ExpectedVersion, operations: new[] { cleanup }, ct: token, lease: operationLease, transfer: transfer))
                throw new FileManagementException("conflict", "迁移版本或租约变化。");
        }, ct);
    }
    /// <summary>按源文件保存的可见性和长度验证目标准入，拒绝时不创建传输或改写源文件。</summary>
    private static async Task ValidateTargetLimitsAsync(FileRecord file, IFileStorage backend, CancellationToken ct)
    {
        if (backend is not IFileStorageLimitProvider provider) return;
        var limits = await provider.GetLimitsAsync(ct);
        if (limits != null && (file.Length > limits.MaximumFileLength ||
            limits.PrivateContainersOnly && file.Visibility != FileVisibility.Private))
            throw new FileManagementException("invalid", "目标存储不支持该文件的可见性或长度。");
    }
    internal async Task DeleteReplicaAsync(FileOperation operation, CancellationToken ct)
    {
        var replica = JsonSerializer.Deserialize<Replica>(operation.Payload) ?? throw new InvalidOperationException("副本参数损坏。");
        var entry = await _store.FindAsync(operation.FileId, ct) ?? throw new FileManagementException("not_found", "文件不存在。");
        if (entry.File.StoreName == replica.StoreName && entry.File.Container == replica.Container && entry.File.ObjectKey == replica.ObjectKey)
            throw new FileManagementException("conflict", "副本仍是当前位置，不能清理。");
        await _storage.GetStorage(replica.StoreName, entry.File.Scope()).DeleteAsync(new FileAddress(replica.Container, replica.ObjectKey), ct);
    }
    public static async Task<string> HashAsync(IFileStorage storage, FileAddress address, CancellationToken ct)
    {
        string result = "";
        await storage.ReadAsync(address, async (input, token) =>
        {
            using var hash = SHA256.Create();
            result = Convert.ToBase64String(await hash.ComputeHashAsync(input, token));
        }, ct);
        return result;
    }
    internal static async Task<Guid?> ValidatePublicTargetAsync(IFileManagementStore store, string targetStore, string scopeKey, CancellationToken ct)
    {
        var marker = targetStore.LastIndexOf("-c-", StringComparison.Ordinal);
        if (marker < 0 || !Guid.TryParseExact(targetStore[(marker + 3)..], "N", out var id)) return null;
        if (store is not IRemoteDriveStateStore connections)
            throw new FileManagementException("invalid", "网盘迁移目标无效。");
        var c = await connections.FindConnectionAsync(id, ct);
        if (c == null || c.ScopeKey != scopeKey || c.StoreName != targetStore || c.Ownership != RemoteDriveOwnership.Shared || c.State != RemoteDriveConnectionState.Active)
            throw new FileManagementException("forbidden", "迁移仅允许当前范围的活动公共连接。");
        return id;
    }
    /// <summary>导入前先预览。每条旧记录必须明确租户/主机归属和根目录内的相对内容路径。</summary>
    public async Task<LegacyFileImport> ImportAsync(LegacyImportRequest request, bool apply, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Source) || request.Source.Length > 100 || request.LegacyId == Guid.Empty)
            throw new ArgumentException("旧数据源与 GUID 必须明确。");
        var mapId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(request.Source + ":" + request.LegacyId.ToString("N"))).AsSpan(0, 16));
        var mapping = await _store.FindImportAsync(mapId, ct);
        if (mapping?.Status == "Imported") return mapping;
        mapping = new LegacyFileImport { Id = mapId, Source = request.Source, LegacyId = request.LegacyId, FileId = request.LegacyId,
            UpdatedAt = FileManagementService.Now, Status = "Preview" };
        try
        {
            if (request.Scope == null) throw new ArgumentException("历史记录没有租户字段，必须提供明确归属。");
            if (!Path.IsPathFullyQualified(request.RootPath) || string.IsNullOrWhiteSpace(request.RelativePath) || Path.IsPathRooted(request.RelativePath) ||
                request.RelativePath.Equals("Test", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("需要旧根目录及有效相对地址映射。");
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(request.RootPath));
            var path = Path.GetFullPath(Path.Combine(root, request.RelativePath));
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new ArgumentException("旧地址越过根目录。");
            for (var node = path; node != null; node = Path.GetDirectoryName(node))
                if ((File.GetAttributes(node) & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("旧路径包含链接。");
            var info = new FileInfo(path);
            if (!info.Exists || info.Length != request.ExpectedLength) throw new ArgumentException("旧文件缺失或长度不一致。");
            if (string.IsNullOrWhiteSpace(request.OwnerId)) throw new ArgumentException("必须指定旧文件所有者。");
            if (string.IsNullOrWhiteSpace(request.FileName) || request.FileName.Length > 255 || request.FileName.Any(char.IsControl)) throw new ArgumentException("旧文件名无效。");
            if (!_options.Containers.TryGetValue(request.Container, out var policy)) throw new ArgumentException("导入容器未配置。");
            FileContentInspector.ValidateDeclaration(policy, Path.GetFileName(request.FileName), request.ContentType, info.Length);
            if (!apply) return mapping;
            var existing = await _store.FindAsync(request.LegacyId, ct);
            var requestKey = "legacy-" + mapId.ToString("N");
            if (existing != null && existing.File.RequestKey != requestKey) throw new ArgumentException("目标 GUID 已被其他文件占用。");
            if (existing != null && existing.File.State != FileState.Ready) throw new ArgumentException("历史映射目标不是 Ready，不得复活已删除记录。");
            if (existing == null)
            {
                var backend = _storage.GetStorage(request.StoreName, request.Scope);
                var connectionId = await ValidatePublicTargetAsync(_store, request.StoreName, request.Scope.Key, ct);
                var file = new FileRecord { Id = request.LegacyId, RequestKey = requestKey, ScopeKey = request.Scope.Key,
                    ScopeKind = request.Scope.Kind, TenantId = request.Scope.TenantId, StoreName = request.StoreName, ConnectionId = connectionId, Container = request.Container,
                    FileName = Path.GetFileName(request.FileName), Length = info.Length, ContentType = request.ContentType,
                    Visibility = FileVisibility.Private, State = FileState.Ready, CreatorId = request.OwnerId, CreatedAt = FileManagementService.Now, Version = 1,
                    ContentPolicyJson = JsonSerializer.Serialize(policy) };
                await _transfers.RunAsync(file, request.StoreName, request.Container, async (attempt, transfer, token) =>
                {
                    var address = new FileAddress(attempt.Container, attempt.ObjectKey);
                    await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, FileOptions.Asynchronous);
                    using var hash = new HashingReadStream(input);
                    await backend.SaveAsync(new FileWriteRequest(address, info.Length, FileWriteMode.Overwrite, request.ContentType), hash, token);
                    await _store.MarkTransferWrittenAsync(transfer, token);
                    var sourceHash = hash.Digest();
                    var properties = await backend.GetPropertiesAsync(address, token);
                    if (properties?.Length != info.Length || sourceHash != await HashAsync(backend, address, token))
                        throw new ArgumentException("导入摘要不一致。");
                    file.ObjectKey = address.Key; file.Sha256 = sourceHash;
                    // 导入文件在发布前执行相同真实内容策略，检查失败由独立目标回收协议清理。
                    await new FileContentInspector(_options).InspectAsync(file, backend, token);
                    if (!await _store.TryCreateAsync(new FileEntry { File = file }, ct: token, transfer: transfer))
                    {
                        var winner = await _store.FindAsync(request.LegacyId, token);
                        if (winner?.File.RequestKey != requestKey || winner.File.State != FileState.Ready)
                            throw new FileManagementException("conflict", "导入提交竞争，请重试。");
                    }
                }, ct);
            }
            mapping.Status = "Imported";
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or FileManagementException or FileStorageException)
        {
            mapping.Status = "NeedsAttention";
            mapping.Issue = ex is FileStorageException storageError ? "存储错误：" + storageError.Error : ex is FileManagementException ? ex.Message : ex is ArgumentException ? ex.Message : "旧文件无法读取。";
        }
        if (apply) await _store.SaveImportAsync(mapping, ct);
        return mapping;
    }
}
public sealed class LegacyImportRequest
{
    public string Source { get; set; } = "";
    public Guid LegacyId { get; set; }
    public StorageScope? Scope { get; set; }
    public string OwnerId { get; set; } = "";
    public string RootPath { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public long ExpectedLength { get; set; }
    public string StoreName { get; set; } = "";
    public string Container { get; set; } = "attachments";
}

/// <summary>边读边计算摘要，不拥有输入流，也不允许 Seek 使摘要产生歧义。</summary>
internal sealed class HashingReadStream : Stream
{
    private readonly Stream _inner;
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    public HashingReadStream(Stream inner) { _inner = inner; }
    public string Digest() => Convert.ToBase64String(_hash.GetHashAndReset());
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override int Read(byte[] buffer, int offset, int count) { var n = _inner.Read(buffer, offset, count); _hash.AppendData(buffer, offset, n); return n; }
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    { var n = await _inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken); _hash.AppendData(buffer, offset, n); return n; }
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    { var n = await _inner.ReadAsync(buffer, cancellationToken); _hash.AppendData(buffer.Span[..n]); return n; }
    protected override void Dispose(bool disposing) { if (disposing) _hash.Dispose(); base.Dispose(disposing); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

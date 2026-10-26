using System.Diagnostics;
using System.Text.Json;
using Bing.FileStorage;

namespace Bing.Biz.FileManagement;

/// <summary>受信管理入口；Web 必须先调用 GetContextAsync(manage:true)，CLI 显式指定范围和操作者。</summary>
public sealed class FileOperationAdministration
{
    private readonly IFileManagementStore _store;
    private readonly IFileStorageFactory _storage;
    public FileOperationAdministration(IFileManagementStore store, IFileStorageFactory storage) { _store = store; _storage = storage; }

    public async Task<IReadOnlyList<FileOperationView>> ListAsync(FileOperationQuery query, CancellationToken ct = default)
    {
        ValidateQuery(query);
        return (await _store.QueryOperationsAsync(query, ct)).Select(View).ToArray();
    }
    public async Task<FileOperationView> GetAsync(string scopeKey, Guid id, CancellationToken ct = default)
        => View(await _store.FindOperationAsync(id, scopeKey, ct) ?? throw new FileManagementException("not_found", "任务不存在。"));

    public async Task<Guid> RecoverAsync(string scopeKey, string actorId, Guid id, RecoverFileOperation request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(scopeKey) || string.IsNullOrWhiteSpace(actorId) || actorId.Length > 200 || request.RequestId == Guid.Empty ||
            request.ExpectedVersion < 1 || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500 || request.Reason.Any(char.IsControl))
            throw new FileManagementException("invalid", "恢复需要范围、操作者、版本、幂等标识和原因。");
        var key = FileManagementService.HashKey(scopeKey + "\n" + actorId + "\n" + request.RequestId.ToString("N"));
        var previous = await _store.FindRecoveryAsync(key, ct);
        if (previous != null)
        {
            if (previous.OperationId != id || previous.Reason != request.Reason) throw new FileManagementException("conflict", "恢复请求号已用于其他操作。");
            return previous.ReplacementId;
        }
        var operation = await _store.FindOperationAsync(id, scopeKey, ct) ?? throw new FileManagementException("not_found", "任务不存在。");
        if (operation.State != FileOperationState.Failed || operation.Version != request.ExpectedVersion || operation.RecoveryOperationId != null)
            throw new FileManagementException("conflict", "仅可恢复尚未生成后继任务的 Failed 版本。");
        var entry = await _store.FindAsync(operation.FileId, ct) ?? throw new FileManagementException("not_found", "文件不存在。");
        if (entry.File.ScopeKey != scopeKey) throw new FileManagementException("not_found", "任务不存在。");
        var payload = operation.Payload;
        switch (operation.Kind)
        {
            case FileOperationKind.Initialize:
            case FileOperationKind.Complete:
            case FileOperationKind.Inspect:
                throw new FileManagementException("new_upload_required", "失败上传已进入清理，请重新创建上传会话。");
            case FileOperationKind.Delete:
                if (entry.File.State != FileState.DeletePending || entry.Bindings.Count != 0) throw Conflict();
                break;
            case FileOperationKind.Abort:
                // 独立 UploadId 补偿不要求整个文件失败，可能另一个初始化执行者已成功。
                if (string.IsNullOrEmpty(payload) && entry.File.State is not (FileState.Aborted or FileState.Failed)) throw Conflict();
                break;
            case FileOperationKind.Migrate:
                if (entry.File.State != FileState.Ready) throw Conflict();
                var target = JsonSerializer.Deserialize<FileMigrationService.Replica>(payload) ?? throw Conflict();
                if (target.StoreName == entry.File.StoreName) throw Conflict();
                _storage.GetStorage(target.StoreName, entry.File.Scope());
                target.ExpectedVersion = entry.File.Version; // 恢复事务不改变文件，只验证版本；复制提交仍用 CAS。
                payload = JsonSerializer.Serialize(target);
                break;
            case FileOperationKind.DeleteReplica:
                var replica = JsonSerializer.Deserialize<FileMigrationService.Replica>(payload) ?? throw Conflict();
                if (entry.File.StoreName == replica.StoreName && entry.File.Container == replica.Container && entry.File.ObjectKey == replica.ObjectKey) throw Conflict();
                break;
            default: throw new FileManagementException("invalid", "该任务不支持人工恢复。");
        }
        var replacement = FileManagementService.Operation(operation.FileId, operation.Kind, payload);
        replacement.ParentOperationId = operation.Id;
        var audit = new FileOperationAudit { Id = Guid.NewGuid(), RequestKey = key, ScopeKey = scopeKey, OperationId = id,
            ReplacementId = replacement.Id, FileId = entry.File.Id, ActorId = actorId, Reason = request.Reason,
            TraceId = Activity.Current?.TraceId.ToString() ?? "", CreatedAt = FileManagementService.Now };
        if (await _store.TryRecoverOperationAsync(operation, entry.File.Version, replacement, audit, ct)) return replacement.Id;
        previous = await _store.FindRecoveryAsync(key, ct);
        if (previous?.OperationId == id && previous.Reason == request.Reason) return previous.ReplacementId;
        throw Conflict();
    }
    private static FileManagementException Conflict() => new("conflict", "文件或任务状态已变化，请重新查询。");
    private static void ValidateQuery(FileOperationQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.ScopeKey) || query.Skip < 0 || query.Take is < 1 or > 100 ||
            query.State.HasValue && !Enum.IsDefined(typeof(FileOperationState), query.State.Value) ||
            query.Kind.HasValue && !Enum.IsDefined(typeof(FileOperationKind), query.Kind.Value) || query.CreatedFrom > query.CreatedTo)
            throw new FileManagementException("invalid", "任务查询条件无效。");
    }
    public static FileOperationView View(FileOperation operation) => new()
    {
        Id = operation.Id, FileId = operation.FileId, Kind = operation.Kind, State = operation.State, Attempts = operation.Attempts,
        CreatedAt = operation.CreatedAt, FinishedAt = operation.FinishedAt, NextAttemptAt = operation.NextAttemptAt, Version = operation.Version,
        ErrorCode = operation.LastError, RecoveryOperationId = operation.RecoveryOperationId,
        RecoveryAction = operation.State != FileOperationState.Failed || operation.RecoveryOperationId != null ? "none" :
            operation.Kind is FileOperationKind.Initialize or FileOperationKind.Complete or FileOperationKind.Inspect ? "new-upload" : "retry-if-current"
    };
}

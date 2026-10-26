using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Biz.FileManagement.AspNetCore;

public abstract partial class FileManagementControllerBase
{
    [HttpGet("diagnostics")]
    public async Task<IActionResult> Diagnostics([FromServices] FileManagementHealthCheck health, CancellationToken ct)
    {
        await _files.GetContextAsync(true, ct);
        // 快照是全库统计，租户管理员不能借管理权限读取其他租户的负载。
        await _files.RequirePermissionAsync("files.host", ct);
        var result = await health.CheckHealthAsync(new(), ct);
        // 复用探针快照；依赖故障时仍能返回诊断状态，避免再次查询故障数据库变成 500。
        result.Data.TryGetValue("statistics", out var statistics);
        return Ok(new { health = result.Status.ToString(), result.Description, statistics });
    }
    [HttpGet("context")]
    public async Task<IActionResult> Context(CancellationToken ct) => Ok(await _files.GetContextAsync(ct: ct));

    [HttpGet("upload-sessions")]
    public async Task<IActionResult> Sessions([FromQuery] FileState? state = null, [FromQuery] bool includeExpired = false,
        [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
        => Ok(await _files.ListSessionsAsync(state, includeExpired, skip, take, ct));

    [HttpGet("by-business")]
    public async Task<IActionResult> BusinessFiles([FromQuery] string businessType, [FromQuery] string businessId,
        [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
        => Ok(await _files.ListBusinessFilesAsync(businessType, businessId, skip, take, ct));

    [HttpGet("{fileId:guid}/bindings")]
    public async Task<IActionResult> Bindings(Guid fileId, CancellationToken ct) => Ok(await _files.ListBindingsAsync(fileId, ct));

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] FileSearchRequest request, CancellationToken ct)
    {
        var context = await _files.GetContextAsync(request.Manage, ct);
        var queryKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        { context.ScopeKey, context.UserId, request.Name, request.Container, request.ContentType, request.CreatedFrom, request.CreatedTo, request.Manage }))));
        var protector = _protector.CreateProtector("FileQuery.v1");
        FileScanPosition? position = null;
        if (request.Cursor != null)
        {
            try
            {
                if (request.Cursor.Length > 4096) throw new ArgumentException();
                var cursor = JsonSerializer.Deserialize<QueryCursor>(protector.Unprotect(request.Cursor));
                if (cursor == null || cursor.QueryKey != queryKey || cursor.ExpiresAt <= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() ||
                    cursor.Position.Key.Length is 0 or > 80) throw new ArgumentException();
                position = cursor.Position;
            }
            catch (Exception ex) when (ex is ArgumentException or CryptographicException or JsonException)
            { throw new FileManagementException("invalid_cursor", "游标无效、过期或不属于当前查询。"); }
        }
        var result = await _files.SearchAsync(request, position, ct);
        var next = result.NextPosition == null ? null : protector.Protect(JsonSerializer.Serialize(new QueryCursor
        { QueryKey = queryKey, Position = result.NextPosition, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeMilliseconds() }));
        return Ok(new { result.Items, nextCursor = next });
    }

    [HttpGet("operations")]
    public async Task<IActionResult> Operations([FromServices] FileOperationAdministration administration,
        [FromQuery] Guid? fileId = null, [FromQuery] FileOperationKind? kind = null, [FromQuery] FileOperationState? state = null,
        [FromQuery] long? createdFrom = null, [FromQuery] long? createdTo = null, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        var context = await _files.GetContextAsync(true, ct);
        return Ok(await administration.ListAsync(new() { ScopeKey = context.ScopeKey, FileId = fileId, Kind = kind, State = state,
            CreatedFrom = createdFrom, CreatedTo = createdTo, Skip = skip, Take = take }, ct));
    }
    [HttpGet("operations/{id:guid}")]
    public async Task<IActionResult> Operation(Guid id, [FromServices] FileOperationAdministration administration, CancellationToken ct)
    { var context = await _files.GetContextAsync(true, ct); return Ok(await administration.GetAsync(context.ScopeKey, id, ct)); }
    [HttpGet("operations/{id:guid}/audit")]
    public async Task<IActionResult> OperationAudit(Guid id, CancellationToken ct)
    { var context = await _files.GetContextAsync(true, ct); return Ok(await _store.ListOperationAuditAsync(context.ScopeKey, id, ct)); }
    [HttpPost("operations/{id:guid}/retry")]
    public async Task<IActionResult> RetryOperation(Guid id, [FromBody] RecoverFileOperation request,
        [FromServices] FileOperationAdministration administration, CancellationToken ct)
    {
        var context = await _files.GetContextAsync(true, ct);
        return Accepted(new { operationId = await administration.RecoverAsync(context.ScopeKey, context.UserId, id, request, ct) });
    }

    [HttpGet("migration-batches")]
    public async Task<IActionResult> Batches([FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
    { var context = await _files.GetContextAsync(true, ct); return Ok(await _store.ListBatchesAsync(context.ScopeKey, Math.Max(0, skip), Math.Clamp(take, 1, 100), ct)); }
    [HttpPost("migration-batches/preview")]
    public async Task<IActionResult> PreviewBatch([FromBody] MigrationPreviewRequest request, [FromServices] FileMigrationBatchService batches, CancellationToken ct)
    {
        var context = await _files.GetContextAsync(true, ct);
        var rows = await batches.PreviewAsync(context.ToStorageScope(), request.Selection, request.Position, ct);
        return Ok(new { items = rows.Select(f => new { f.Id, f.FileName, f.Length, f.Version }),
            nextPosition = rows.Count == 100 ? new FileScanPosition { CreatedAt = rows[^1].CreatedAt, Key = rows[^1].RequestKey } : null });
    }
    [HttpPost("migration-batches")]
    public async Task<IActionResult> CreateBatch([FromBody] CreateMigrationBatch request, [FromServices] FileMigrationBatchService batches, CancellationToken ct)
    { var context = await _files.GetContextAsync(true, ct); return Accepted(await batches.CreateAsync(context.ToStorageScope(), context.UserId, request, ct)); }
    [HttpGet("migration-batches/{id:guid}")]
    public async Task<IActionResult> Batch(Guid id, [FromServices] FileMigrationBatchService batches, CancellationToken ct)
    { var context = await _files.GetContextAsync(true, ct); return Ok(await batches.GetAsync(context.ScopeKey, id, ct)); }
    [HttpGet("migration-batches/{id:guid}/items")]
    public async Task<IActionResult> BatchItems(Guid id, [FromServices] FileMigrationBatchService batches, [FromQuery] FileMigrationItemState? state = null,
        [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        var context = await _files.GetContextAsync(true, ct); await batches.GetAsync(context.ScopeKey, id, ct);
        if (state.HasValue && !Enum.IsDefined(typeof(FileMigrationItemState), state.Value)) throw new FileManagementException("invalid", "迁移项状态无效。");
        return Ok(await _store.ListBatchItemsAsync(id, state, Math.Max(0, skip), Math.Clamp(take, 1, 100), ct));
    }
    [HttpPost("migration-batches/{id:guid}/pause")]
    public async Task<IActionResult> PauseBatch(Guid id, [FromBody] BatchVersionRequest request, [FromServices] FileMigrationBatchService batches, CancellationToken ct)
    { var context = await _files.GetContextAsync(true, ct); await batches.SetPausedAsync(context.ScopeKey, id, request.ExpectedVersion, true, ct); return NoContent(); }
    [HttpPost("migration-batches/{id:guid}/resume")]
    public async Task<IActionResult> ResumeBatch(Guid id, [FromBody] BatchVersionRequest request, [FromServices] FileMigrationBatchService batches, CancellationToken ct)
    { var context = await _files.GetContextAsync(true, ct); await batches.SetPausedAsync(context.ScopeKey, id, request.ExpectedVersion, false, ct); return NoContent(); }
    [HttpPost("migration-batches/{id:guid}/retry-failed")]
    public async Task<IActionResult> RetryBatch(Guid id, [FromBody] BatchVersionRequest request, [FromServices] FileMigrationBatchService batches,
        [FromServices] FileOperationAdministration administration, CancellationToken ct)
    {
        var context = await _files.GetContextAsync(true, ct);
        return Accepted(new { queued = await batches.RetryFailedAsync(context.ScopeKey, context.UserId, id, request.ExpectedVersion, request.Reason, administration, ct) });
    }
    private sealed class QueryCursor
    {
        public string QueryKey { get; set; } = "";
        public FileScanPosition Position { get; set; } = new();
        public long ExpiresAt { get; set; }
    }
    public sealed class BatchVersionRequest { public long ExpectedVersion { get; set; } public string Reason { get; set; } = ""; }
    public sealed class MigrationPreviewRequest
    {
        public CreateMigrationBatch Selection { get; set; } = new();
        public FileScanPosition? Position { get; set; }
    }
}

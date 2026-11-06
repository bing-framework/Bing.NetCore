using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Biz.FileManagement.AspNetCore;

public abstract partial class FileManagementControllerBase
{
    [HttpGet("capabilities")]
    public async Task<IActionResult> Capabilities([FromServices] IFilePermissionChecker permissions, CancellationToken ct)
    {
        var context = await _files.GetContextAsync(ct: ct);
        return Ok(new { context, manage = await permissions.IsGrantedAsync("files.manage", ct),
            manageConnections = await permissions.IsGrantedAsync("files.connections.manage", ct),
            upload = await permissions.IsGrantedAsync("files.upload", ct), containers = _options.Containers.Keys,
            historyRetentionDays = _options.HistoryRetentionDays });
    }
    [HttpGet("events")]
    public async Task<IActionResult> Events([FromServices] FileGovernanceAdministration administration, [FromQuery] Guid? fileId = null,
        [FromQuery] bool? delivered = null, [FromQuery] bool failedOnly = false, [FromQuery] bool includeArchived = false,
        [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        var context = await _files.GetContextAsync(true, ct);
        return Ok(await administration.ListEventsAsync(new() { ScopeKey = context.ScopeKey, FileId = fileId, Delivered = delivered,
            FailedOnly = failedOnly, IncludeArchived = includeArchived, Skip = skip, Take = take }, ct));
    }
    [HttpGet("events/{id:guid}")]
    public async Task<IActionResult> Event(Guid id, [FromServices] FileGovernanceAdministration administration, CancellationToken ct)
    {
        var context = await _files.GetContextAsync(true, ct);
        var result = await administration.GetEventAsync(context.ScopeKey, id, ct);
        return Ok(new { result.Event, result.Deliveries, consumers = administration.Consumers });
    }
    [HttpPost("events/{id:guid}/replay")]
    public async Task<IActionResult> ReplayEvent(Guid id, [FromBody] ReplayFileEvent request, [FromServices] FileGovernanceAdministration administration, CancellationToken ct)
    {
        var context = await _files.GetContextAsync(true, ct);
        return Accepted(await administration.ReplayAsync(context.ScopeKey, context.UserId, id, request, ct));
    }
    [HttpPost("history/preview")]
    public async Task<IActionResult> PreviewHistory([FromBody] HistoryPreviewRequest request, [FromServices] FileGovernanceAdministration administration, CancellationToken ct)
    {
        var context = await _files.GetContextAsync(true, ct);
        var selection = await administration.PreviewAsync(context.ScopeKey, request.Kind, request.RetentionDays, request.Take, ct);
        var token = _protector.CreateProtector("FileHistoryPreview.v1").Protect(JsonSerializer.Serialize(new HistoryPreviewToken
        { ScopeKey = context.ScopeKey, ActorId = context.UserId, Selection = selection, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeMilliseconds() }));
        return Ok(new { selection, token });
    }
    [HttpPost("history/archive")]
    public async Task<IActionResult> ArchiveHistory([FromBody] HistoryArchiveRequest request, [FromServices] FileGovernanceAdministration administration, CancellationToken ct)
    {
        var context = await _files.GetContextAsync(true, ct);
        HistoryPreviewToken preview;
        try
        {
            if (string.IsNullOrEmpty(request.Token) || request.Token.Length > 65536) throw new ArgumentException();
            preview = JsonSerializer.Deserialize<HistoryPreviewToken>(_protector.CreateProtector("FileHistoryPreview.v1").Unprotect(request.Token)) ?? throw new ArgumentException();
            if (preview.ScopeKey != context.ScopeKey || preview.ActorId != context.UserId || preview.ExpiresAt <= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) throw new ArgumentException();
        }
        catch (Exception ex) when (ex is ArgumentException or CryptographicException or JsonException)
        { throw new FileManagementException("invalid_preview", "归档预览无效、过期或不属于当前用户。"); }
        return Ok(await administration.ArchiveAsync(context.ScopeKey, context.UserId, preview.Selection,
            new() { RequestId = request.RequestId, Reason = request.Reason }, ct));
    }
    [HttpGet("history")]
    public async Task<IActionResult> History([FromServices] FileGovernanceAdministration administration, [FromQuery] FileHistoryKind? kind = null,
        [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        var context = await _files.GetContextAsync(true, ct);
        return Ok(await administration.ListArchivesAsync(context.ScopeKey, kind, skip, take, ct));
    }
    [HttpPost("history/{id:guid}/restore")]
    public async Task<IActionResult> RestoreHistory(Guid id, [FromBody] FileGovernanceRequest request, [FromServices] FileGovernanceAdministration administration, CancellationToken ct)
    {
        var context = await _files.GetContextAsync(true, ct);
        return Ok(await administration.RestoreAsync(context.ScopeKey, context.UserId, id, request, ct));
    }
    [HttpGet("governance-audit")]
    public async Task<IActionResult> GovernanceAudit([FromServices] FileGovernanceAdministration administration, [FromQuery] Guid? targetId = null,
        [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        var context = await _files.GetContextAsync(true, ct);
        return Ok(await administration.ListAuditAsync(context.ScopeKey, targetId, skip, take, ct));
    }
    public sealed class HistoryPreviewRequest
    { public FileHistoryKind Kind { get; set; } public int? RetentionDays { get; set; } public int Take { get; set; } = 20; }
    public sealed class HistoryArchiveRequest
    { public Guid RequestId { get; set; } public string Reason { get; set; } = ""; public string Token { get; set; } = ""; }
    private sealed class HistoryPreviewToken
    {
        public string ScopeKey { get; set; } = "";
        public string ActorId { get; set; } = "";
        public long ExpiresAt { get; set; }
        public FileHistorySelection Selection { get; set; } = new();
    }
}

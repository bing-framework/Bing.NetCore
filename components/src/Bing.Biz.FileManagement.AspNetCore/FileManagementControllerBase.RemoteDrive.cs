using Bing.FileStorage;
using Bing.FileStorage.RemoteDrive;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Biz.FileManagement.AspNetCore;

public abstract partial class FileManagementControllerBase
{
    public sealed class ConnectionStartRequest
    {
        public string Template { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public RemoteDriveOwnership Ownership { get; set; }
        public string RedirectUri { get; set; } = "";
        public Guid? ReconnectId { get; set; }
    }
    public sealed class ConnectionFinishRequest
    {
        public string State { get; set; } = "";
        public string? Code { get; set; }
    }
    public sealed class StoragePreferenceRequest
    {
        public string Container { get; set; } = "attachments";
        public string TargetId { get; set; } = "";
        public bool ScopeDefault { get; set; }
    }
    public sealed class BindingCopyRequest
    {
        public Guid RequestId { get; set; }
        public string BusinessType { get; set; } = "";
        public string BusinessId { get; set; } = "";
    }
    [HttpGet("storage-targets")]
    public async Task<IActionResult> StorageTargets([FromServices] FileStorageConnectionService connections,
        [FromServices] IEnumerable<RemoteDriveTemplate> templates, [FromServices] IEnumerable<IFileStorageBackend> stores,
        CancellationToken ct, [FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        await _files.RequirePermissionAsync("files.upload", ct);
        take = Math.Clamp(take, 1, 100); skip = Math.Max(0, skip);
        var rows = await connections.ListAsync(skip: skip, take: take, ct: ct);
        return Ok(new { defaultStore = _storage.DefaultStore, stores = stores.Select(s => s.Name).OrderBy(n => n).ToArray(),
            templates = templates.Select(t => new { name = t.Name, provider = t.Client.Provider }),
            connections = rows.Where(c => c.State == RemoteDriveConnectionState.Active), nextSkip = rows.Count == take ? (int?)(skip + take) : null });
    }
    [HttpGet("storage-connections")]
    public async Task<IActionResult> Connections([FromServices] FileStorageConnectionService connections, CancellationToken ct,
        [FromQuery] bool manage = false, [FromQuery] int skip = 0, [FromQuery] int take = 50)
        => Ok(await connections.ListAsync(manage, skip, take, ct));
    [HttpPost("storage-connections/authorize")]
    public async Task<IActionResult> StartConnection([FromBody] ConnectionStartRequest request, [FromServices] FileStorageConnectionService connections, CancellationToken ct)
    {
        var result = await connections.StartAsync(request.Template, request.DisplayName, request.Ownership, request.RedirectUri, request.ReconnectId, ct);
        if (!string.IsNullOrEmpty(result.AuthorizationUrl))
        {
            using var data = QRCoder.QRCodeGenerator.GenerateQrCode(result.AuthorizationUrl, QRCoder.QRCodeGenerator.ECCLevel.M);
            using var png = new QRCoder.PngByteQRCode(data);
            result.AuthorizationQrPng = Convert.ToBase64String(png.GetGraphic(4));
        }
        Response.Headers["Cache-Control"] = "no-store";
        return Ok(result);
    }
    [HttpPost("storage-connections/{id:guid}/authorize")]
    public async Task<IActionResult> CompleteConnection(Guid id, [FromBody] ConnectionFinishRequest request, [FromServices] FileStorageConnectionService connections, CancellationToken ct)
        => Ok(await connections.CompleteAsync(id, request.State, request.Code, ct));
    [HttpDelete("storage-connections/{id:guid}")]
    public async Task<IActionResult> DisableConnection(Guid id, [FromServices] FileStorageConnectionService connections, CancellationToken ct)
    { await connections.DisableAsync(id, ct); return NoContent(); }
    [HttpPut("storage-preference")]
    public async Task<IActionResult> Preference([FromBody] StoragePreferenceRequest request, [FromServices] FileStorageConnectionService connections, CancellationToken ct)
    { await connections.SetPreferenceAsync(request.Container, request.TargetId, request.ScopeDefault, ct); return NoContent(); }
    [HttpPost("{fileId:guid}/binding-copies")]
    public async Task<IActionResult> CreateBindingCopy(Guid fileId, [FromBody] BindingCopyRequest request, [FromServices] FileBindingCopyService copies, CancellationToken ct)
        => Accepted(await copies.CreateAsync(fileId, request.RequestId, request.BusinessType, request.BusinessId, ct));
    [HttpGet("binding-copies/{id:guid}")]
    public async Task<IActionResult> BindingCopy(Guid id, [FromServices] FileBindingCopyService copies, CancellationToken ct) => Ok(await copies.GetAsync(id, ct));
    [HttpPost("binding-copies/{id:guid}/bind")]
    public async Task<IActionResult> BindCopy(Guid id, [FromServices] FileBindingCopyService copies, CancellationToken ct) => Ok(new { fileId = await copies.BindAsync(id, ct) });
    [HttpDelete("binding-copies/{id:guid}")]
    public async Task<IActionResult> CancelBindingCopy(Guid id, [FromServices] FileBindingCopyService copies, CancellationToken ct) { await copies.CancelAsync(id, ct); return Accepted(); }
}

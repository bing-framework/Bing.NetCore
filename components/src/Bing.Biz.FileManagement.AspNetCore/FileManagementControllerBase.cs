using System.Security.Cryptography;
using System.Text.Json;
using Bing.FileStorage;
using Bing.FileStorage.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Biz.FileManagement.AspNetCore;

/// <summary>宿主通过派生控制器接入同一组 API；所有身份、业务和租户校验都在应用服务执行。</summary>
[ApiController]
[Authorize]
[Route("api/files")]
[ServiceFilter(typeof(FileManagementExceptionFilter))]
public abstract partial class FileManagementControllerBase : ControllerBase
{
    private readonly FileManagementService _files;
    private readonly IFileManagementStore _store;
    private readonly IFileStorageFactory _storage;
    private readonly FileManagementOptions _options;
    private readonly IDataProtector _protector;
    protected FileManagementControllerBase(FileManagementService files, IFileManagementStore store, IFileStorageFactory storage,
        FileManagementOptions options, IDataProtectionProvider protection)
    { _files = files; _store = store; _storage = storage; _options = options; _protector = protection.CreateProtector("Bing.FileManagement.Download.v1"); }
    [HttpPost("upload-sessions")]
    public async Task<IActionResult> Create([FromBody] CreateFileUpload request, CancellationToken ct)
        => Accepted(await _files.CreateUploadAsync(request, ct));
    [HttpGet("upload-sessions/{id:guid}")]
    public async Task<IActionResult> Session(Guid id, CancellationToken ct, [FromQuery] bool includeParts = true)
        => Ok(await _files.GetSessionAsync(id, includeParts, ct));
    [HttpPost("upload-sessions/{id:guid}/part-requests")]
    public async Task<IActionResult> PartRequest(Guid id, [FromBody] PartUploadRequest request, CancellationToken ct)
    {
        var signed = await _files.CreatePartRequestAsync(id, request, ct);
        if (signed != null) return Ok(signed);
        return Ok(new { url = Request.PathBase + $"/api/files/upload-sessions/{id}/parts/{request.Number}", method = "PUT",
            expiresAt = DateTimeOffset.UtcNow.Add(_options.PartSignatureLifetime), headers = new Dictionary<string, string>() });
    }
    [HttpPut("upload-sessions/{id:guid}/parts/{number:int}")]
    [RequestSizeLimit(128L * 1024 * 1024)]
    public async Task<IActionResult> UploadPart(Guid id, int number, CancellationToken ct)
        => Ok(await _files.UploadPartAsync(id, number, Request.Body, ct));
    [HttpPost("upload-sessions/{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct) => Accepted(await _files.CompleteUploadAsync(id, ct));
    [HttpDelete("upload-sessions/{id:guid}")]
    public async Task<IActionResult> Abort(Guid id, CancellationToken ct) { await _files.AbortUploadAsync(id, ct); return Accepted(); }
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int skip = 0, [FromQuery] int take = 50, [FromQuery] bool manage = false, CancellationToken ct = default)
        => Ok(await _files.ListAsync(skip, take, manage, ct));
    [HttpGet("{fileId:guid}")]
    public async Task<IActionResult> Details(Guid fileId, CancellationToken ct) => Ok(await _files.GetAsync(fileId, ct));
    [HttpGet("{fileId:guid}/content")]
    [HttpHead("{fileId:guid}/content")]
    public async Task<IActionResult> Content(Guid fileId, CancellationToken ct)
        => Download((await _files.AuthorizeReadAsync(fileId, ct: ct)).File);
    [AllowAnonymous]
    [HttpGet("public/{fileId:guid}/content")]
    [HttpHead("public/{fileId:guid}/content")]
    public async Task<IActionResult> PublicContent(Guid fileId, CancellationToken ct)
        => Download((await _files.AuthorizeReadAsync(fileId, true, ct)).File);
    [HttpPost("{fileId:guid}/download-link")]
    public async Task<IActionResult> DownloadLink(Guid fileId, CancellationToken ct)
    {
        var file = (await _files.AuthorizeReadAsync(fileId, ct: ct)).File;
        var storage = _storage.GetStorage(file.StoreName, file.Scope());
        if (storage.Capabilities.HasFlag(FileStorageCapabilities.SignedDownload))
            return Ok(await ((ISignedFileRequestProvider)storage).SignDownloadAsync(file.Address(), _options.DownloadSignatureLifetime, file.FileName, ct));
        var expires = DateTimeOffset.UtcNow.Add(_options.DownloadSignatureLifetime);
        var token = _protector.Protect(JsonSerializer.Serialize(new DownloadGrant { Id = file.Id, Version = file.Version, Expires = expires.ToUnixTimeMilliseconds() }));
        return Ok(new { url = Request.PathBase + $"/api/files/{file.Id}/signed-content?token={Uri.EscapeDataString(token)}", method = "GET", expiresAt = expires });
    }
    [AllowAnonymous]
    [HttpGet("{fileId:guid}/signed-content")]
    public async Task<IActionResult> SignedContent(Guid fileId, [FromQuery] string token, CancellationToken ct)
    {
        DownloadGrant? grant;
        try { grant = JsonSerializer.Deserialize<DownloadGrant>(_protector.Unprotect(token)); }
        catch (Exception ex) when (ex is CryptographicException or JsonException or ArgumentException) { return NotFound(); }
        if (grant == null || grant.Id != fileId || grant.Expires <= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) return NotFound();
        var entry = await _store.FindAsync(fileId, ct);
        if (entry?.File.State != FileState.Ready || entry.File.Version != grant.Version) return NotFound();
        return Download(entry.File);
    }
    [HttpPost("{fileId:guid}/bindings")]
    public async Task<IActionResult> Bind(Guid fileId, [FromBody] BindingRequest request, CancellationToken ct)
        => Ok(await _files.BindAsync(fileId, request.BusinessType, request.BusinessId, ct));
    [HttpDelete("{fileId:guid}/bindings/{bindingId:guid}")]
    public async Task<IActionResult> Unbind(Guid fileId, Guid bindingId, CancellationToken ct)
    { await _files.UnbindAsync(fileId, bindingId, ct); return NoContent(); }
    [HttpDelete("{fileId:guid}")]
    public async Task<IActionResult> Delete(Guid fileId, CancellationToken ct)
    { await _files.DeleteAsync(fileId, ct); return Accepted(); }
    private IActionResult Download(FileRecord file)
        => new StoredFileResult(_storage.GetStorage(file.StoreName, file.Scope()), file.Address(), file.FileName, file.ContentType);
    private sealed class DownloadGrant { public Guid Id { get; set; } public long Version { get; set; } public long Expires { get; set; } }
    public sealed class BindingRequest { public string BusinessType { get; set; } = ""; public string BusinessId { get; set; } = ""; }
}

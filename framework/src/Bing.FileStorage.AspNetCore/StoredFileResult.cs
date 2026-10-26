using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Bing.FileStorage.AspNetCore;

/// <summary>不缓冲全文件的下载结果。只支持一个字节区间；响应中始终以附件方式处理文件名。</summary>
public sealed class StoredFileResult : IActionResult
{
    private readonly IFileStorage _storage;
    private readonly FileAddress _address;
    private readonly string _name;
    private readonly string _type;
    public StoredFileResult(IFileStorage storage, FileAddress address, string name, string? contentType)
    { _storage = storage; _address = address; _name = name; _type = contentType ?? "application/octet-stream"; }
    public async Task ExecuteResultAsync(ActionContext context)
    {
        var http = context.HttpContext; var response = http.Response; var ct = http.RequestAborted;
        var properties = await _storage.GetPropertiesAsync(_address, ct);
        if (properties == null) { response.StatusCode = 404; return; }
        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.Headers[HeaderNames.CacheControl] = "private, no-store";
        var disposition = new ContentDispositionHeaderValue("attachment");
        disposition.SetHttpFileName(_name);
        response.Headers[HeaderNames.ContentDisposition] = disposition.ToString();
        response.ContentType = MediaTypeHeaderValue.TryParse(_type, out var type) ? type.ToString() : "application/octet-stream";
        response.Headers[HeaderNames.LastModified] = properties.LastModifiedUtc.ToString("R", CultureInfo.InvariantCulture);
        var etag = properties.ETag == null ? null : "\"" + properties.ETag.Trim('"').Replace("\r", "").Replace("\n", "") + "\"";
        if (etag != null) response.Headers[HeaderNames.ETag] = etag;
        var supportsRange = _storage.Capabilities.HasFlag(FileStorageCapabilities.Range);
        if (supportsRange) response.Headers[HeaderNames.AcceptRanges] = "bytes";
        var rangeText = http.Request.Headers[HeaderNames.Range].ToString();
        var ifRange = http.Request.Headers[HeaderNames.IfRange].ToString();
        var useRange = supportsRange && rangeText.Length > 0;
        if (ifRange.Length > 0 && ifRange != etag)
            useRange = useRange && DateTimeOffset.TryParse(ifRange, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) &&
                properties.LastModifiedUtc.ToUnixTimeSeconds() <= date.ToUnixTimeSeconds();
        long offset = 0, length = properties.Length;
        if (useRange)
        {
            if (!RangeHeaderValue.TryParse(rangeText, out var range) || range.Unit != "bytes" || range.Ranges.Count != 1 || properties.Length == 0)
            { Unsatisfied(response, properties.Length); return; }
            var item = range.Ranges.Single();
            if (item.From.HasValue)
            {
                offset = item.From.Value;
                if (offset >= properties.Length || item.To < offset) { Unsatisfied(response, properties.Length); return; }
                length = Math.Min(item.To ?? (properties.Length - 1), properties.Length - 1) - offset + 1;
            }
            else
            {
                if (!item.To.HasValue || item.To.Value == 0) { Unsatisfied(response, properties.Length); return; }
                length = Math.Min(item.To.Value, properties.Length); offset = properties.Length - length;
            }
            response.StatusCode = StatusCodes.Status206PartialContent;
            response.Headers[HeaderNames.ContentRange] = $"bytes {offset}-{offset + length - 1}/{properties.Length}";
        }
        response.ContentLength = length;
        if (HttpMethods.IsHead(http.Request.Method)) return;
        if (useRange) await ((IFileRangeReader)_storage).ReadRangeAsync(_address, offset, length, (input, token) => input.CopyToAsync(response.Body, 128 * 1024, token), ct);
        else await _storage.ReadAsync(_address, (input, token) => input.CopyToAsync(response.Body, 128 * 1024, token), ct);
    }
    private static void Unsatisfied(HttpResponse response, long total)
    { response.StatusCode = StatusCodes.Status416RangeNotSatisfiable; response.Headers[HeaderNames.ContentRange] = $"bytes */{total}"; response.ContentLength = 0; }
}

public static class FormFileStorageExtensions
{
    /// <summary>用于小表单文件；大文件应使用持久分片会话。底层上传失败时不会接管 IFormFile 的生命周期。</summary>
    public static async Task<FileWriteResult> SaveFormFileAsync(this IFileStorage storage, FileAddress address, IFormFile file,
        long maximumLength, FileWriteMode mode, CancellationToken ct = default)
    {
        if (file.Length > maximumLength) throw new ArgumentOutOfRangeException(nameof(file), "表单文件超过限制。");
        await using var input = file.OpenReadStream();
        return await storage.SaveAsync(new FileWriteRequest(address, file.Length, mode, file.ContentType), input, ct);
    }
}

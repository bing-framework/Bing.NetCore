using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Bing.FileStorage.AspNetCore;

/// <summary>
/// 支持单个字节范围的流式文件下载结果。
/// </summary>
/// <remarks>
/// 不缓冲全文件的下载结果。只支持一个字节区间；响应中始终以附件方式处理文件名。
/// </remarks>
public sealed class StoredFileResult : IActionResult
{
    /// <summary>
    /// 保存用于读取文件内容的内部文件存储。
    /// </summary>
    private readonly IFileStorage _storage;
    /// <summary>
    /// 保存待返回文件在存储中的地址。
    /// </summary>
    private readonly FileAddress _address;
    /// <summary>
    /// 保存下载响应的文件名。
    /// </summary>
    private readonly string _name;
    /// <summary>
    /// 保存下载响应的媒体类型。
    /// </summary>
    private readonly string _type;
    /// <summary>
    /// 初始化一个 <see cref="StoredFileResult" /> 类型的实例。
    /// </summary>
    /// <param name="storage">实例使用的存储实现。</param>
    /// <param name="address">文件在逻辑容器中的地址。</param>
    /// <param name="name">实例使用的名称。</param>
    /// <param name="contentType">内容的媒体类型；未指定时使用默认类型。</param>
    public StoredFileResult(IFileStorage storage, FileAddress address, string name, string? contentType)
    { _storage = storage; _address = address; _name = name; _type = contentType ?? "application/octet-stream"; }
    /// <inheritdoc />
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
    /// <summary>
    /// 设置无效范围响应的 HTTP 状态和 Content-Range 标头。
    /// </summary>
    /// <param name="response">要更新的 HTTP 响应。</param>
    /// <param name="total">完整资源的总字节数。</param>
    private static void Unsatisfied(HttpResponse response, long total)
    { response.StatusCode = StatusCodes.Status416RangeNotSatisfiable; response.Headers[HeaderNames.ContentRange] = $"bytes */{total}"; response.ContentLength = 0; }
}

/// <summary>
/// 提供从表单文件保存到文件存储的扩展方法。
/// </summary>
public static class FormFileStorageExtensions
{
    /// <summary>
    /// 将表单文件保存到文件存储。
    /// </summary>
    /// <param name="storage">目标文件存储。</param>
    /// <param name="address">目标文件地址。</param>
    /// <param name="file">要保存的表单文件。</param>
    /// <param name="maximumLength">允许的最大文件长度。</param>
    /// <param name="mode">文件写入模式。</param>
    /// <param name="ct">用于取消读取和写入的令牌。</param>
    /// <returns>文件存储返回的写入结果。</returns>
    /// <remarks>
    /// 文件长度超过 maximumLength 时抛出参数异常；方法负责释放表单文件读取流。
    /// </remarks>
    public static async Task<FileWriteResult> SaveFormFileAsync(this IFileStorage storage, FileAddress address, IFormFile file,
        long maximumLength, FileWriteMode mode, CancellationToken ct = default)
    {
        if (file.Length > maximumLength) throw new ArgumentOutOfRangeException(nameof(file), "表单文件超过限制。");
        await using var input = file.OpenReadStream();
        return await storage.SaveAsync(new FileWriteRequest(address, file.Length, mode, file.ContentType), input, ct);
    }
}

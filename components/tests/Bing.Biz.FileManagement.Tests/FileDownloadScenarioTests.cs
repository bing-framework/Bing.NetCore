using System.Text;
using Bing.FileStorage;
using Bing.FileStorage.AspNetCore;
using Bing.FileStorage.FileSystem;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

/// <summary>用真实本地内容验证下载协议场景，不以控制器 Mock 替代字节读取。</summary>
public sealed class FileDownloadScenarioTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bing-download-scenarios", Guid.NewGuid().ToString("N"));
    private readonly FileAddress _address = new("attachments", "scenario");

    /// <summary>闭合、开放、后缀及越界请求必须返回准确字节和完整 Content-Range。</summary>
    [Theory]
    [InlineData("bytes=1-3", 206, "bcd", "bytes 1-3/5")]
    [InlineData("bytes=3-", 206, "de", "bytes 3-4/5")]
    [InlineData("bytes=-2", 206, "de", "bytes 3-4/5")]
    [InlineData("bytes=0-99", 206, "abcde", "bytes 0-4/5")]
    [InlineData("bytes=5-", 416, "", "bytes */5")]
    [InlineData("bytes=-0", 416, "", "bytes */5")]
    [InlineData("bytes=0-1,3-4", 416, "", "bytes */5")]
    public async Task Range_ReturnsExactBytesAndHeaders(string range, int status, string body, string contentRange)
    {
        var storage = await Seed("abcde");
        var http = Context(); http.Request.Headers.Range = range;
        await Execute(storage, http);
        Assert.Equal(status, http.Response.StatusCode);
        Assert.Equal(contentRange, http.Response.Headers.ContentRange.ToString());
        Assert.Equal(body, Encoding.UTF8.GetString(((MemoryStream)http.Response.Body).ToArray()));
        Assert.Equal(Encoding.UTF8.GetByteCount(body), http.Response.ContentLength);
        Assert.Equal("nosniff", http.Response.Headers["X-Content-Type-Options"]);
        Assert.Equal("private, no-store", http.Response.Headers.CacheControl.ToString());
    }

    /// <summary>过期 If-Range 回退为完整内容，HEAD 只返回头部。</summary>
    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    public async Task StaleIfRange_ReturnsFullMetadataAndHeadHasNoBody(string method)
    {
        var storage = await Seed("abcde");
        var http = Context(); http.Request.Method = method;
        http.Request.Headers.Range = "bytes=1-2"; http.Request.Headers.IfRange = "\"stale\"";
        await Execute(storage, http);
        Assert.Equal(200, http.Response.StatusCode);
        Assert.Equal(5, http.Response.ContentLength);
        Assert.Equal("", http.Response.Headers.ContentRange.ToString());
        Assert.Equal(method == "HEAD" ? "" : "abcde", Encoding.UTF8.GetString(((MemoryStream)http.Response.Body).ToArray()));
        Assert.Contains("filename*=UTF-8''", http.Response.Headers.ContentDisposition.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>空文件普通下载成功，范围请求不可满足。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyFile_HasZeroBody(bool range)
    {
        var storage = await Seed(""); var http = Context();
        if (range) http.Request.Headers.Range = "bytes=0-";
        await Execute(storage, http);
        Assert.Equal(range ? 416 : 200, http.Response.StatusCode);
        Assert.Equal(0, http.Response.ContentLength);
        Assert.Equal(0, http.Response.Body.Length);
    }

    private async Task<IFileStorage> Seed(string text)
    {
        var storage = new FileSystemStorageBackend("local", _root).Bind(StorageScope.Host);
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(text));
        await storage.SaveAsync(new(_address, input.Length, FileWriteMode.CreateOnly), input);
        return storage;
    }
    private static DefaultHttpContext Context()
    { var http = new DefaultHttpContext(); http.Request.Method = "GET"; http.Response.Body = new MemoryStream(); return http; }
    private Task Execute(IFileStorage storage, HttpContext http) => new StoredFileResult(storage, _address, "用户资料.txt", "text/plain")
        .ExecuteResultAsync(new ActionContext(http, new RouteData(), new ActionDescriptor()));
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}

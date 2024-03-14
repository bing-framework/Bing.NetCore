using Bing.Content;
using Microsoft.AspNetCore.Mvc;

namespace Bing.AspNetCore.Mvc.ContentFormatters;

/// <summary>
/// 远程流内容 - 测试 - 控制器
/// </summary>
[Route("api/remote-stream-content-test")]
public class RemoteStreamContentTestController : ApiControllerBase
{
    /// <summary>
    /// 测试 - 下载
    /// </summary>
    [HttpGet("Download")]
    public async Task<IRemoteStreamContent> DownloadAsync()
    {
        var memoryStream = new MemoryStream();
        await memoryStream.WriteAsync(Encoding.UTF8.GetBytes("DownloadAsync"));
        memoryStream.Position = 0;
        return new RemoteStreamContent(memoryStream, "download.rtf", "application/rtf");
    }
}

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

    /// <summary>
    /// 测试 - 下载 - 自定义内容
    /// </summary>
    [HttpGet("Download-With-Custom-Content-Disposition")]
    public async Task<IRemoteStreamContent> Download_With_Custom_Content_Disposition_Async()
    {
        var memoryStream = new MemoryStream();
        await memoryStream.WriteAsync(Encoding.UTF8.GetBytes("DownloadAsync"));
        memoryStream.Position = 0;
        Response.Headers.Add("Content-Disposition", "attachment; filename=myDownload.rtf");
        return new RemoteStreamContent(memoryStream, "download.rtf", "application/rtf");
    }

    /// <summary>
    /// 测试 - 下载 - 中文文件名
    /// </summary>
    [HttpGet("Download_With_Chinese_File_Name")]
    public async Task<IRemoteStreamContent> Download_With_Chinese_File_Name_Async()
    {
        var memoryStream = new MemoryStream();
        await memoryStream.WriteAsync(Encoding.UTF8.GetBytes("DownloadAsync"));
        memoryStream.Position = 0;
        return new RemoteStreamContent(memoryStream, "下载文件.rtf", "application/rtf");
    }

    /// <summary>
    /// 测试 - 上传
    /// </summary>
    /// <param name="file">文件</param>
    [HttpPost("Upload")]
    public async Task<string> UploadAsync(IRemoteStreamContent file)
    {
        using var reader = new StreamReader(file.GetStream());
        return await reader.ReadToEndAsync() + ":" + file.ContentType + ":" + file.FileName;
    }

    /// <summary>
    /// 测试 - 上传 - 流
    /// </summary>
    /// <param name="file">文件</param>
    [HttpPost("Upload-Raw")]
    public async Task<string> UploadRawAsync(IRemoteStreamContent file)
    {
        using var reader = new StreamReader(file.GetStream());
        return await reader.ReadToEndAsync() + ":" + file.ContentType;
    }
}

using System.Net.Http;
using System.Net.Http.Headers;

namespace Bing.AspNetCore.Mvc.ContentFormatters;

public class RemoteStreamContentTestControllerTest : BingAspNetCoreTestBase
{
    /// <summary>
    /// 测试初始化
    /// </summary>
    public RemoteStreamContentTestControllerTest(HttpClient client)
    {
        InitClient(client);
    }

    /// <summary>
    /// 测试 - 下载
    /// </summary>
    [Fact]
    public async Task Test_DownloadAsync()
    {
        var result = await GetResponseAsync("/api/remote-stream-content-test/download");
        result.Content.Headers.ContentType?.ToString().ShouldBe("application/rtf");
        result.Content.Headers.ContentDisposition?.FileName.ShouldBe("download.rtf");
        result.Content.Headers.ContentLength.ShouldBe("DownloadAsync".Length);
        (await result.Content.ReadAsStringAsync()).ShouldBe("DownloadAsync");
    }

    /// <summary>
    /// 测试 - 下载 - 自定义内容
    /// </summary>
    [Fact]
    public async Task Test_Download_With_Custom_Content_Disposition_Async()
    {
        var result = await GetResponseAsync("/api/remote-stream-content-test/download-with-custom-content-disposition");
        result.Content.Headers.ContentType?.ToString().ShouldBe("application/rtf");
        result.Content.Headers.ContentDisposition?.FileName.ShouldBe("myDownload.rtf");
        result.Content.Headers.ContentLength.ShouldBe("DownloadAsync".Length);
        (await result.Content.ReadAsStringAsync()).ShouldBe("DownloadAsync");
    }

    /// <summary>
    /// 测试 - 下载 - 中文文件名
    /// </summary>
    [Fact]
    public async Task Test_Download_With_Chinese_File_Name_Async()
    {
        var result = await GetResponseAsync("/api/remote-stream-content-test/download_with_chinese_file_name");
        result.Content.Headers.ContentType?.ToString().ShouldBe("application/rtf");
        result.Content.Headers.ContentDisposition?.FileNameStar.ShouldBe("下载文件.rtf");
        result.Content.Headers.ContentLength.ShouldBe("DownloadAsync".Length);
        (await result.Content.ReadAsStringAsync()).ShouldBe("DownloadAsync");
    }

    /// <summary>
    /// 测试 - 上传
    /// </summary>
    [Fact]
    public async Task UploadAsync()
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/remote-stream-content-test/upload");
        var memoryStream = new MemoryStream();
        await memoryStream.WriteAsync(Encoding.UTF8.GetBytes("UploadAsync"));
        memoryStream.Position = 0;

        var streamContent = new StreamContent(memoryStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/rtf");
        requestMessage.Content = new MultipartFormDataContent { { streamContent, "file", "upload.rtf" } };

        var response = await Client.SendAsync(requestMessage);

        (await response.Content.ReadAsStringAsync()).ShouldBe("UploadAsync:application/rtf:upload.rtf");
    }

    /// <summary>
    /// 测试 - 上传 - 流
    /// </summary>
    /// <returns></returns>
    [Fact(Skip = "Http Status Code 415")]
    public async Task UploadRawAsync()
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/remote-stream-content-test/upload-raw");
        var memoryStream = new MemoryStream();
        var text = @"{ ""hello"": ""world"" }";
        await memoryStream.WriteAsync(Encoding.UTF8.GetBytes(text));
        memoryStream.Position = 0;

        var streamContent = new StreamContent(memoryStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        requestMessage.Content = streamContent;

        var response = await Client.SendAsync(requestMessage);
        (await response.Content.ReadAsStringAsync()).ShouldBe($"{text}:application/json");
    }
}

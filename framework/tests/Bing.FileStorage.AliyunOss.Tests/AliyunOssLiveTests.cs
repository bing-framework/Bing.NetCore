using System.Text;
using Bing.FileStorage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.FileStorage.AliyunOss.Tests;

/// <summary>
/// 覆盖无参构造函数创建后，Value 应为类型默认值（引用类型为 null）等测试场景。
/// </summary>
public sealed class AliyunOssLiveTests
{
    /// <summary>
    /// 验证配置的 OSS 存储桶支持保存、读取及删除。
    /// </summary>
    [AliyunOssLiveFact]
    public async Task SaveReadDeleteAgainstConfiguredBucket()
    {
        var services = new ServiceCollection();
        services.AddBingFileStorage(options => options.DefaultStore = "aliyun-oss-live");
        services.AddBingAliyunOssStorage("aliyun-oss-live", options =>
        {
            options.Region = Required("BING_FILE_STORAGE_OSS_REGION");
            options.Endpoint = Environment.GetEnvironmentVariable("BING_FILE_STORAGE_OSS_ENDPOINT");
            options.AccessKeyId = Required("BING_FILE_STORAGE_OSS_ACCESS_KEY_ID");
            options.AccessKeySecret = Required("BING_FILE_STORAGE_OSS_ACCESS_KEY_SECRET");
            options.SecurityToken = Environment.GetEnvironmentVariable("BING_FILE_STORAGE_OSS_SECURITY_TOKEN");
            options.ContainerBuckets["attachments"] = Required("BING_FILE_STORAGE_OSS_BUCKET");
            options.StorageNamespace = "bing-tests";
        });

        using var provider = services.BuildServiceProvider();
        var storage = provider.GetRequiredService<IFileStorageFactory>().GetStorage("aliyun-oss-live", StorageScope.Host);
        var address = new FileAddress("attachments", "live-" + Guid.NewGuid().ToString("N"));
        var bytes = Encoding.UTF8.GetBytes("Bing Aliyun OSS live contract probe");
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        try
        {
            using var source = new MemoryStream(bytes, writable: false);
            var saved = await storage.SaveAsync(new FileWriteRequest(address, bytes.Length, FileWriteMode.Overwrite), source);
            Assert.Equal(bytes.Length, saved.Properties.Length);
            using var read = new MemoryStream();
            await storage.ReadAsync(address, (stream, _) => stream.CopyToAsync(read));
            Assert.Equal(bytes, read.ToArray());
            var multipart = (IMultipartFileStorage)storage;
            var upload = await multipart.CreateMultipartAsync(new FileWriteRequest(address, bytes.Length, FileWriteMode.Overwrite), 5 * 1024 * 1024, deadline.Token);
            var part = await multipart.UploadPartAsync(upload, 1, new MemoryStream(bytes), cancellationToken: deadline.Token);
            Assert.Equal(part.ETag, (await ((IMultipartFilePartReader)storage).GetPartAsync(upload, 1, deadline.Token))!.ETag);
            await multipart.CompleteMultipartAsync(upload, new[] { part }, deadline.Token);
            Assert.Equal(bytes.Length, (await multipart.CompleteMultipartAsync(upload, new[] { part }, deadline.Token)).Properties.Length);
            using var range = new MemoryStream();
            await ((IFileRangeReader)storage).ReadRangeAsync(address, 1, 4, (stream, ct) => stream.CopyToAsync(range, ct), deadline.Token);
            Assert.Equal(bytes.Skip(1).Take(4), range.ToArray());
            var link = await ((ISignedFileRequestProvider)storage).SignDownloadAsync(address, TimeSpan.FromMinutes(1), "附件.txt", deadline.Token);
            using var http = new HttpClient();
            Assert.Equal(bytes, await http.GetByteArrayAsync(link.Url, deadline.Token));
            var abandoned = await multipart.CreateMultipartAsync(new FileWriteRequest(new("attachments", address.Key + "-abort"), bytes.Length, FileWriteMode.Overwrite), 5 * 1024 * 1024, deadline.Token);
            await multipart.AbortMultipartAsync(abandoned, deadline.Token);
        }
        finally
        {
            await storage.DeleteAsync(address);
        }
    }

    /// <summary>
    /// 读取必需的实机测试环境变量。
    /// </summary>
    /// <param name="name">必需环境配置的名称。</param>
    /// <returns>必需环境变量的配置值；缺失时抛出异常。</returns>
    private static string Required(string name) =>
        Environment.GetEnvironmentVariable(name) ?? throw new InvalidOperationException("Missing required live test setting " + name + ".");
}

/// <summary>
/// 仅在配置真实 OSS 测试环境时运行集成测试。
/// </summary>
internal sealed class AliyunOssLiveFactAttribute : FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="AliyunOssLiveFactAttribute" /> 类型的实例。
    /// </summary>
    public AliyunOssLiveFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("BING_FILE_STORAGE_OSS_RUN_LIVE") != "1")
            Skip = "未启用独立 OSS 测试 Bucket；本地协议测试不代表云端兼容通过。";
    }
}

using System.Net;
using System.Security.Cryptography;
using Bing.FileStorage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.FileStorage.S3.Tests;

/// <summary>
/// 覆盖回源并发和批量载荷遵守资源限制等测试场景。
/// </summary>
public sealed class MinioRecoveryTests : IClassFixture<MinioBucketFixture>
{
    /// <summary>初始化桶后运行恢复用例，使该测试类可以单独执行。</summary>
    public MinioRecoveryTests(MinioBucketFixture fixture) { }

    /// <summary>
    /// 验证 MinIO 完成上传校验 ETag、保持幂等并拒绝后续分片。
    /// </summary>
    [MinioFact]
    public async Task Complete_ValidatesETag_IsIdempotent_AndRejectsLateParts()
    {
        using var services = CreateServices();
        var storage = services.GetRequiredService<IFileStorageFactory>().GetStorage("minio", StorageScope.Host);
        var multipart = (IMultipartFileStorage)storage;
        var address = new FileAddress("attachments", "recovery-" + Guid.NewGuid().ToString("N"));
        var bytes = new byte[] { 1, 2, 3 };
        var upload = await multipart.CreateMultipartAsync(new(address, bytes.Length, FileWriteMode.Overwrite), 5 * 1024 * 1024);
        try
        {
            var part = await multipart.UploadPartAsync(upload, 1, new MemoryStream(bytes));
            var wrong = new FilePart { Number = 1, Length = 3, ETag = "wrong", Sha256 = part.Sha256 };
            Assert.Equal(FileStorageError.InvalidPart, (await Assert.ThrowsAsync<FileStorageException>(
                () => multipart.CompleteMultipartAsync(upload, new[] { wrong }))).Error);
            await multipart.CompleteMultipartAsync(upload, new[] { part });
            // 模拟第一次完成响应没有交给调用者，使用持久句柄再次完成。
            var repeated = await multipart.CompleteMultipartAsync(upload, new[] { part });
            Assert.Equal(3, repeated.Properties.Length);
            Assert.Equal(FileStorageError.UploadNotFound, (await Assert.ThrowsAsync<FileStorageException>(
                () => multipart.UploadPartAsync(upload, 1, new MemoryStream(bytes)))).Error);
        }
        finally { await multipart.AbortMultipartAsync(upload); await storage.DeleteAsync(address); }
    }

    /// <summary>
    /// 验证预签名分片按期过期且不可更改校验和。
    /// </summary>
    [MinioFact]
    public async Task PresignedPart_Expires_AndCannotChangeChecksum()
    {
        using var services = CreateServices();
        var storage = services.GetRequiredService<IFileStorageFactory>().GetStorage("minio", StorageScope.Host);
        var multipart = (IMultipartFileStorage)storage; var signer = (ISignedFileRequestProvider)storage;
        var address = new FileAddress("attachments", "signed-" + Guid.NewGuid().ToString("N"));
        var bytes = new byte[] { 4, 5, 6 };
        var hash = Convert.ToBase64String(SHA256.HashData(bytes));
        var upload = await multipart.CreateMultipartAsync(new(address, 3, FileWriteMode.Overwrite), 5 * 1024 * 1024);
        try
        {
            using var http = new HttpClient();
            var signed = await signer.SignPartAsync(upload, 1, hash, TimeSpan.FromMinutes(1));
            using var changed = new HttpRequestMessage(HttpMethod.Put, signed.Url) { Content = new ByteArrayContent(bytes) };
            changed.Headers.TryAddWithoutValidation("x-amz-checksum-sha256", Convert.ToBase64String(new byte[32]));
            using var rejected = await http.SendAsync(changed);
            Assert.False(rejected.IsSuccessStatusCode);
            var expired = await signer.SignPartAsync(upload, 1, hash, TimeSpan.FromSeconds(1));
            await Task.Delay(2200);
            using var request = new HttpRequestMessage(HttpMethod.Put, expired.Url) { Content = new ByteArrayContent(bytes) };
            request.Headers.TryAddWithoutValidation("x-amz-checksum-sha256", hash);
            using var response = await http.SendAsync(request);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Empty(await multipart.ListPartsAsync(upload));
        }
        finally { await multipart.AbortMultipartAsync(upload); await storage.DeleteAsync(address); }
    }
    /// <summary>
    /// 创建 MinIO 恢复实机测试容器。
    /// </summary>
    /// <returns>注册了当前测试依赖的服务容器。</returns>
    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection(); services.AddBingFileStorage(o => o.DefaultStore = "minio");
        services.AddBingMinioStorage("minio", o => {
            o.Endpoint = Environment.GetEnvironmentVariable("BING_FILE_STORAGE_MINIO_ENDPOINT")!;
            o.AccessKey = Environment.GetEnvironmentVariable("BING_FILE_STORAGE_MINIO_ACCESS_KEY")!;
            o.SecretKey = Environment.GetEnvironmentVariable("BING_FILE_STORAGE_MINIO_SECRET_KEY")!;
            o.StorageNamespace = "recovery-tests";
        });
        return services.BuildServiceProvider();
    }
}

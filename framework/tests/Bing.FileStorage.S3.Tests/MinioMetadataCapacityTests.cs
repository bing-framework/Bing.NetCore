using Bing.FileStorage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.FileStorage.S3.Tests;

/// <summary>
/// 覆盖注册幂等、保留预注册实现，并且默认管理器按 DI scope 隔离等测试场景。
/// </summary>
public sealed class MinioMetadataCapacityTests : IClassFixture<MinioBucketFixture>
{
    /// <summary>等待异步夹具准备桶，不依赖其它实机测试先执行。</summary>
    public MinioMetadataCapacityTests(MinioBucketFixture fixture) { }

    /// <summary>
    /// 验证 MinIO 为不同大小的对象持久化十六项元数据。
    /// </summary>
    [MinioFact]
    public async Task Minio_SaveAsync_PersistsSixteenMetadataItemsForEmptyAndNonEmptyObjects()
    {
        var endpoint = Environment.GetEnvironmentVariable("BING_FILE_STORAGE_MINIO_ENDPOINT")
            ?? throw new InvalidOperationException("真实 MinIO 测试端点未配置。");
        var accessKey = Environment.GetEnvironmentVariable("BING_FILE_STORAGE_MINIO_ACCESS_KEY") ?? "bingtest";
        var secretKey = Environment.GetEnvironmentVariable("BING_FILE_STORAGE_MINIO_SECRET_KEY") ?? "bingtest-password-2026";
        var services = new ServiceCollection();
        services.AddBingFileStorage(options => options.DefaultStore = "minio-metadata-capacity");
        services.AddBingMinioStorage("minio-metadata-capacity", options =>
        {
            options.Endpoint = endpoint;
            options.AccessKey = accessKey;
            options.SecretKey = secretKey;
            options.StorageNamespace = "metadata-capacity-tests";
        });

        using var provider = services.BuildServiceProvider();
        var storage = provider.GetRequiredService<IFileStorageFactory>()
            .GetStorage("minio-metadata-capacity", StorageScope.Host);
        var prefix = "metadata-capacity-" + Guid.NewGuid().ToString("N");
        var emptyAddress = new FileAddress("attachments", prefix + "/empty");
        var contentAddress = new FileAddress("attachments", prefix + "/content");
        var metadata = Enumerable.Range(0, 16)
            .ToDictionary(index => $"meta{index:D2}", index => $"v{index:D2}", StringComparer.Ordinal);
        var content = new byte[] { 1, 3, 5, 7, 9, 11, 13 };

        try
        {
            var emptyResult = await storage.SaveAsync(
                new FileWriteRequest(emptyAddress, 0, FileWriteMode.Overwrite, "application/octet-stream", metadata),
                Stream.Null);
            Assert.Equal(0, emptyResult.Properties.Length);
            AssertMetadata(metadata, emptyResult.Properties.Metadata);

            using var contentSource = new MemoryStream(content, writable: false);
            var contentResult = await storage.SaveAsync(
                new FileWriteRequest(contentAddress, content.Length, FileWriteMode.Overwrite, "application/octet-stream", metadata),
                contentSource);
            Assert.Equal((long)content.Length, contentResult.Properties.Length);
            AssertMetadata(metadata, contentResult.Properties.Metadata);

            using var read = new MemoryStream();
            await storage.ReadAsync(contentAddress, async (stream, cancellationToken) =>
                await stream.CopyToAsync(read, cancellationToken));
            Assert.Equal(content, read.ToArray());
        }
        finally
        {
            await storage.DeleteAsync(emptyAddress);
            await storage.DeleteAsync(contentAddress);
        }
    }

    /// <summary>
    /// 验证 MinIO 对象元数据与预期一致。
    /// </summary>
    /// <param name="expected">预期的断言结果。</param>
    /// <param name="actual">实际观测值。</param>
    private static void AssertMetadata(IReadOnlyDictionary<string, string> expected, IReadOnlyDictionary<string, string> actual)
    {
        Assert.Equal(expected.Count, actual.Count(pair => !FileWriteRequest.IsMultipartMetadata(pair.Key)));
        foreach (var pair in expected)
            Assert.Equal(pair.Value, actual[pair.Key]);
    }
}

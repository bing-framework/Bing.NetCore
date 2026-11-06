using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Xunit;

namespace Bing.FileStorage.S3.Tests;

/// <summary>在每个实机测试类开始前准备共享桶，不依赖其它测试的执行顺序。</summary>
public sealed class MinioBucketFixture : IAsyncLifetime
{
    /// <summary>初始化已配置的隔离 MinIO；未配置时由实机测试属性负责跳过。</summary>
    public async Task InitializeAsync()
    {
        var endpoint = Environment.GetEnvironmentVariable("BING_FILE_STORAGE_MINIO_ENDPOINT");
        if (string.IsNullOrWhiteSpace(endpoint)) return;

        using var client = new AmazonS3Client(new BasicAWSCredentials(
            Environment.GetEnvironmentVariable("BING_FILE_STORAGE_MINIO_ACCESS_KEY") ?? "bingtest",
            Environment.GetEnvironmentVariable("BING_FILE_STORAGE_MINIO_SECRET_KEY") ?? "bingtest-password-2026"),
            new AmazonS3Config
            {
                ServiceURL = endpoint, ForcePathStyle = true, AuthenticationRegion = "us-east-1",
                UseHttp = new Uri(endpoint).Scheme == Uri.UriSchemeHttp
            });
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await EnsureBucketAsync(client, deadline.Token);
    }

    /// <summary>不删除共享桶；各测试清理自己的对象，隔离容器由验收脚本回收。</summary>
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>确认桶可访问，仅在明确不存在时创建；其它错误直接传播。</summary>
    internal static async Task EnsureBucketAsync(IAmazonS3 client, CancellationToken ct = default)
    {
        try
        {
            await client.GetBucketLocationAsync(new GetBucketLocationRequest { BucketName = "attachments" }, ct);
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchBucket")
        {
            try
            {
                await client.PutBucketAsync(new PutBucketRequest { BucketName = "attachments" }, ct);
            }
            catch (AmazonS3Exception creation) when (creation.ErrorCode == "BucketAlreadyOwnedByYou")
            {
                // 其它并行类或进程已建好同一桶；再次确认可访问，不能吞掉权限或服务错误。
                await client.GetBucketLocationAsync(new GetBucketLocationRequest { BucketName = "attachments" }, ct);
            }
        }
    }
}

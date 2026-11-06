using Amazon.Runtime;
using Amazon.S3;
using Bing.FileStorage;
using Xunit;

namespace Bing.FileStorage.S3.Tests;

/// <summary>
/// 覆盖初始化 PostgreSQL 集成测试所需的专用 schema 和表等测试场景。
/// </summary>
public sealed class S3ProviderContractTests
{
    /// <summary>
    /// 验证 S3 不支持的仅新建写模式在读取源流前被拒绝。
    /// </summary>
    [Fact]
    public async Task CreateOnly_IsRejectedBeforeReadingTheCallerStream()
    {
        using var client = new AmazonS3Client(new BasicAWSCredentials("test-access", "test-secret"), new AmazonS3Config
        {
            ServiceURL = "http://127.0.0.1:19000",
            ForcePathStyle = true,
            AuthenticationRegion = "us-east-1",
            UseHttp = true
        });
        var storage = new S3FileStorageBackend("s3-test", "test", client).Bind(StorageScope.Host);
        var source = new CountingNonSeekStream();
        var request = new FileWriteRequest(new FileAddress("attachments", "contract/object"), 1,
            FileWriteMode.CreateOnly);

        var error = await Assert.ThrowsAsync<FileStorageException>(() => storage.SaveAsync(request, source));

        Assert.Equal(FileStorageError.UnsupportedCapability, error.Error);
        Assert.Equal(FileStorageCapabilities.None, storage.Capabilities & FileStorageCapabilities.CreateOnly);
        Assert.Equal(0, source.ReadCalls);
    }

    /// <summary>
    /// 验证 MinIO 注册要求显式凭据。
    /// </summary>
    [Fact]
    public void MinioRegistration_RequiresExplicitCredentials()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddBingMinioStorage("minio-test", options =>
        {
            options.Endpoint = "http://127.0.0.1:19000";
        }));
    }

    /// <summary>
    /// 包装CountingNonSeek流并提供对应的读取行为。
    /// </summary>
    private sealed class CountingNonSeekStream : Stream
    {
        /// <summary>
        /// 获取读取流的累计调用次数。
        /// </summary>
        public int ReadCalls { get; private set; }
        /// <inheritdoc />
        public override bool CanRead => true;
        /// <inheritdoc />
        public override bool CanSeek => false;
        /// <inheritdoc />
        public override bool CanWrite => false;
        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();
        /// <inheritdoc />
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) { ReadCalls++; return 0; }
        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { ReadCalls++; return ValueTask.FromResult(0); }
        /// <inheritdoc />
        public override void Flush() { }
        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();
        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

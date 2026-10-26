using Amazon.Runtime;
using Amazon.S3;
using Bing.FileStorage;
using Xunit;

namespace Bing.FileStorage.S3.Tests;

public sealed class S3ProviderContractTests
{
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

    [Fact]
    public void MinioRegistration_RequiresExplicitCredentials()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddBingMinioStorage("minio-test", options =>
        {
            options.Endpoint = "http://127.0.0.1:19000";
        }));
    }

    private sealed class CountingNonSeekStream : Stream
    {
        public int ReadCalls { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) { ReadCalls++; return 0; }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { ReadCalls++; return ValueTask.FromResult(0); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

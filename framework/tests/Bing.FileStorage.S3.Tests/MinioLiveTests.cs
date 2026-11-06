using System.Security.Cryptography;
using System.Text;
using Amazon.S3;
using Bing.FileStorage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.FileStorage.S3.Tests;

/// <summary>
/// 仅在配置 MinIO 测试端点时运行真实服务测试。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class MinioFactAttribute : FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="MinioFactAttribute" /> 类型的实例。
    /// </summary>
    public MinioFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BING_FILE_STORAGE_MINIO_ENDPOINT")))
            Skip = "未配置 BING_FILE_STORAGE_MINIO_ENDPOINT，跳过真实 MinIO 测试。";
    }
}

/// <summary>
/// 组织MinioLive相关测试场景。
/// </summary>
public sealed class MinioLiveTests : IClassFixture<MinioBucketFixture>
{
    /// <summary>由 xUnit 等待桶初始化完成后再构造测试，保留类之间的并行执行。</summary>
    public MinioLiveTests(MinioBucketFixture fixture) { }

    /// <summary>
    /// 验证 MinIO 基础读写、分片、范围及签名请求。
    /// </summary>
    [MinioFact]
    public async Task Minio_BasicMultipartRangeAndSignedRequests_RoundTrip()
    {
        var endpoint = Environment.GetEnvironmentVariable("BING_FILE_STORAGE_MINIO_ENDPOINT")
            ?? throw new InvalidOperationException("真实 MinIO 测试端点未配置。");

        var accessKey = Environment.GetEnvironmentVariable("BING_FILE_STORAGE_MINIO_ACCESS_KEY") ?? "bingtest";
        var secretKey = Environment.GetEnvironmentVariable("BING_FILE_STORAGE_MINIO_SECRET_KEY") ?? "bingtest-password-2026";
        var services = new ServiceCollection();
        services.AddBingFileStorage(options => options.DefaultStore = "minio-test");
        services.AddBingMinioStorage("minio-test", options =>
        {
            options.Endpoint = endpoint;
            options.AccessKey = accessKey;
            options.SecretKey = secretKey;
            options.StorageNamespace = "contract";
            options.ListPartsPageSize = 1;
        });

        using var provider = services.BuildServiceProvider();
        var storage = provider.GetRequiredService<IFileStorageFactory>().GetStorage("minio-test", StorageScope.Host);
        var keyPrefix = "provider-test-" + Guid.NewGuid().ToString("N");
        var basicAddress = new FileAddress("attachments", keyPrefix + "/basic");
        var emptyAddress = new FileAddress("attachments", keyPrefix + "/empty");
        var multipartAddress = new FileAddress("attachments", keyPrefix + "/multipart");
        var invalidAddress = new FileAddress("attachments", keyPrefix + "/invalid");
        var abortAddress = new FileAddress("attachments", keyPrefix + "/abort");
        var largeAddress = new FileAddress("attachments", keyPrefix + "/large-save");
        var basicData = Encoding.UTF8.GetBytes("S3 stream ownership and metadata");
        var multipartData = new byte[5 * 1024 * 1024 + 37];
        RandomNumberGenerator.Fill(multipartData);

        try
        {
            using (var basicSource = new NonSeekReadStream(basicData))
            {
                var result = await storage.SaveAsync(new FileWriteRequest(basicAddress, basicData.Length,
                    FileWriteMode.Overwrite, "text/plain", new Dictionary<string, string> { ["bing-session"] = "basic-session" }), basicSource);
                Assert.True(basicSource.CanRead);
                Assert.Equal(basicData.Length, result.Properties.Length);
                Assert.Equal("basic-session", result.Properties.Metadata["bing-session"]);
            }

            using (var emptySource = new NonSeekReadStream(Array.Empty<byte>()))
            {
                var result = await storage.SaveAsync(new FileWriteRequest(emptyAddress, 0, FileWriteMode.Overwrite), emptySource);
                Assert.Equal(0, result.Properties.Length);
                Assert.True(emptySource.CanRead);
            }

            var invalidData = Encoding.UTF8.GetBytes("declared length is too short");
            using var invalidSource = new NonSeekReadStream(invalidData);
            var lengthError = await Assert.ThrowsAsync<FileStorageException>(() => storage.SaveAsync(
                new FileWriteRequest(invalidAddress, invalidData.Length - 1, FileWriteMode.Overwrite),
                invalidSource));
            Assert.Equal(FileStorageError.InvalidLength, lengthError.Error);
            Assert.Null(await storage.GetPropertiesAsync(invalidAddress));

            const long largeLength = 128L * 1024 * 1024 + 37;
            using (var largeSource = new GeneratedNonSeekReadStream(largeLength))
            {
                var result = await storage.SaveAsync(new FileWriteRequest(largeAddress, largeLength,
                    FileWriteMode.Overwrite, "application/octet-stream"), largeSource);
                Assert.Equal(largeLength, result.Properties.Length);
                Assert.True(largeSource.CanRead);
                using var tail = new MemoryStream();
                await ((IFileRangeReader)storage).ReadRangeAsync(largeAddress, largeLength - 37, 37,
                    (stream, cancellationToken) => stream.CopyToAsync(tail, 81920, cancellationToken));
                Assert.Equal(new byte[37].Select(_ => (byte)0x5a), tail.ToArray());
            }

            using (var read = new MemoryStream())
            {
                await storage.CopyToAsync(basicAddress, read);
                Assert.Equal(basicData, read.ToArray());
            }

            using (var ranged = new MemoryStream())
            {
                await ((IFileRangeReader)storage).ReadRangeAsync(basicAddress, 7, 12,
                    (stream, cancellationToken) => stream.CopyToAsync(ranged, 81920, cancellationToken));
                Assert.Equal(basicData.Skip(7).Take(12), ranged.ToArray());
            }

            var signedDownload = await ((ISignedFileRequestProvider)storage).SignDownloadAsync(basicAddress,
                TimeSpan.FromMinutes(2), "中文 name.txt");
            using (var http = new HttpClient())
            using (var response = await http.GetAsync(signedDownload.Url))
            {
                response.EnsureSuccessStatusCode();
                Assert.Equal(basicData, await response.Content.ReadAsByteArrayAsync());
            }

            var callbackFailure = new AmazonS3Exception("consumer failure");
            var readFailure = await Assert.ThrowsAsync<AmazonS3Exception>(() => storage.ReadAsync(basicAddress,
                (_, _) => Task.FromException(callbackFailure)));
            Assert.Same(callbackFailure, readFailure);
            var rangeFailure = await Assert.ThrowsAsync<AmazonS3Exception>(() =>
                ((IFileRangeReader)storage).ReadRangeAsync(basicAddress, 0, 1,
                    (_, _) => Task.FromException(callbackFailure)));
            Assert.Same(callbackFailure, rangeFailure);

            var multipart = (IMultipartFileStorage)storage;
            var abortUpload = await multipart.CreateMultipartAsync(
                new FileWriteRequest(abortAddress, 1, FileWriteMode.Overwrite), 5 * 1024 * 1024);
            await multipart.AbortMultipartAsync(abortUpload);
            await multipart.AbortMultipartAsync(abortUpload);
            var abortedError = await Assert.ThrowsAsync<FileStorageException>(() => multipart.ListPartsAsync(abortUpload));
            Assert.Equal(FileStorageError.UploadNotFound, abortedError.Error);
            Assert.Equal(FileStorageError.UploadNotFound,
                (await Assert.ThrowsAsync<FileStorageException>(() => multipart.GetPartAsync(abortUpload, 1))).Error);

            var request = new FileWriteRequest(multipartAddress, multipartData.Length, FileWriteMode.Overwrite,
                "application/octet-stream", new Dictionary<string, string> { ["bing-session"] = "multipart-session" });
            var upload = await multipart.CreateMultipartAsync(request, 5 * 1024 * 1024);
            Assert.Empty(await multipart.ListPartsAsync(upload));
            Assert.Null(await multipart.GetPartAsync(upload, 1));
            var signedProvider = (ISignedFileRequestProvider)storage;
            var partLength = upload.GetPartLength(1);
            var partHash = Convert.ToBase64String(SHA256.HashData(multipartData.AsSpan(0, (int)partLength)));
            var partRequest = await signedProvider.SignPartAsync(upload, 1, partHash, TimeSpan.FromMinutes(2));

            using (var http = new HttpClient())
            {
                using var content = new ByteArrayContent(multipartData.AsSpan(0, (int)partLength).ToArray());
                using var put = new HttpRequestMessage(HttpMethod.Put, partRequest.Url) { Content = content };
                put.Headers.TryAddWithoutValidation("x-amz-checksum-sha256", partHash);
                using var response = await http.SendAsync(put);
                response.EnsureSuccessStatusCode();
                Assert.Equal(partHash, (await multipart.GetPartAsync(upload, 1))!.Sha256);
                Assert.Null(await multipart.GetPartAsync(upload, 2));

                var lastOffset = (int)partLength;
                var lastData = multipartData.AsSpan(lastOffset).ToArray();
                var lastHash = Convert.ToBase64String(SHA256.HashData(lastData));
                var lastRequest = await signedProvider.SignPartAsync(upload, 2, lastHash, TimeSpan.FromMinutes(2));
                using var lastContent = new ByteArrayContent(lastData);
                using var lastPut = new HttpRequestMessage(HttpMethod.Put, lastRequest.Url) { Content = lastContent };
                lastPut.Headers.TryAddWithoutValidation("x-amz-checksum-sha256", lastHash);
                using var lastResponse = await http.SendAsync(lastPut);
                lastResponse.EnsureSuccessStatusCode();
            }

            var listed = await multipart.ListPartsAsync(upload);
            Assert.Equal(2, listed.Count);
            Assert.Equal(partHash, listed[0].Sha256);
            Assert.Equal(2, listed[1].Number);
            Assert.Equal(listed[1].ETag, (await multipart.GetPartAsync(upload, 2))!.ETag);
            var completed = await multipart.CompleteMultipartAsync(upload, listed);
            Assert.Equal(multipartData.Length, completed.Properties.Length);
            Assert.Equal("multipart-session", completed.Properties.Metadata["bing-session"]);

            using (var read = new MemoryStream())
            {
                await storage.CopyToAsync(multipartAddress, read);
                Assert.Equal(multipartData, read.ToArray());
            }

            Assert.Null(await storage.GetPropertiesAsync(new FileAddress("attachments", keyPrefix + "/missing")));
            await storage.DeleteAsync(basicAddress);
            await storage.DeleteAsync(emptyAddress);
            await storage.DeleteAsync(multipartAddress);
            await storage.DeleteAsync(abortAddress);
            await storage.DeleteAsync(largeAddress);
        }
        finally
        {
            await storage.DeleteAsync(basicAddress);
            await storage.DeleteAsync(emptyAddress);
            await storage.DeleteAsync(multipartAddress);
            await storage.DeleteAsync(abortAddress);
            await storage.DeleteAsync(largeAddress);
        }
    }

    /// <summary>
    /// 包装NonSeekRead流并提供对应的读取行为。
    /// </summary>
    private sealed class NonSeekReadStream : Stream
    {
        /// <summary>
        /// 保存不可定位读取流包装的内存流。
        /// </summary>
        private readonly MemoryStream _inner;
        /// <summary>
        /// 初始化一个 <see cref="NonSeekReadStream" /> 类型的实例。
        /// </summary>
        /// <param name="bytes">当前内容的字节数或字节数据。</param>
        public NonSeekReadStream(byte[] bytes) => _inner = new MemoryStream(bytes, writable: false);
        /// <inheritdoc />
        public override bool CanRead => _inner.CanRead;
        /// <inheritdoc />
        public override bool CanSeek => false;
        /// <inheritdoc />
        public override bool CanWrite => false;
        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();
        /// <inheritdoc />
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);
        /// <inheritdoc />
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _inner.ReadAsync(buffer, offset, count, cancellationToken);
        /// <inheritdoc />
        public override void Flush() { }
        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();
        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _inner.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// 包装GeneratedNonSeekRead流并提供对应的读取行为。
    /// </summary>
    private sealed class GeneratedNonSeekReadStream : Stream
    {
        /// <summary>
        /// 记录生成式不可定位读取流剩余可读取的字节数。
        /// </summary>
        private long _remaining;

        /// <summary>
        /// 初始化一个 <see cref="GeneratedNonSeekReadStream" /> 类型的实例。
        /// </summary>
        /// <param name="length">内容的长度，单位为字节。</param>
        public GeneratedNonSeekReadStream(long length) => _remaining = length;
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
        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = (int)Math.Min(count, _remaining);
            Array.Fill(buffer, (byte)0x5a, offset, read);
            _remaining -= read;
            return read;
        }

        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = (int)Math.Min(buffer.Length, _remaining);
            buffer.Span[..read].Fill(0x5a);
            _remaining -= read;
            return ValueTask.FromResult(read);
        }

        /// <inheritdoc />
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Read(buffer, offset, count));
        }

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

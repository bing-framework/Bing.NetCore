using System.Security.Cryptography;
using System.Text;
using Bing.FileStorage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.FileStorage.AliyunOss.Tests;

/// <summary>
/// 覆盖注册幂等、保留预注册实现，并且默认管理器按 DI scope 隔离等测试场景。
/// </summary>
public sealed class AliyunOssProtocolTests
{
    /// <summary>
    /// 定义阿里云 OSS 协议测试使用的分片大小。
    /// </summary>
    private const int PartSize = 5 * 1024 * 1024;

    /// <summary>
    /// 验证 OSS 存取、范围读取和删除使用映射键且保留输入流所有权。
    /// </summary>
    [Fact]
    public async Task SaveReadRangePropertiesAndDelete_UseMappedBucketScopedKeyAndPreserveSourceOwnership()
    {
        using var handler = new FakeOssHandler();
        using var client = FakeOssHandler.CreateClient(handler);
        var scope = StorageScope.ForTenant("tenant-a");
        var storage = Backend(client).Bind(scope);
        var address = new FileAddress("attachments", "folder/object");
        var bytes = Encoding.UTF8.GetBytes("oss payload");
        var source = new NonSeekReadStream(bytes);

        var saved = await storage.SaveAsync(new FileWriteRequest(address, bytes.Length, FileWriteMode.Overwrite,
            "text/plain", new Dictionary<string, string> { ["source"] = "test", ["bing-session"] = "business-session" }), source);

        Assert.False(source.IsDisposed);
        Assert.Equal(FileStorageCapabilities.Multipart | FileStorageCapabilities.SignedDownload |
            FileStorageCapabilities.Range | FileStorageCapabilities.ServerVerifiedParts, storage.Capabilities);
        Assert.Equal("aliyun-oss", saved.Location.StoreName);
        Assert.Equal(scope, saved.Location.Scope);
        Assert.Equal(bytes.Length, saved.Properties.Length);
        Assert.Equal("text/plain", saved.Properties.ContentType);
        Assert.Equal("test", saved.Properties.Metadata["source"]);
        Assert.Equal("business-session", saved.Properties.Metadata["bing-session"]);
        Assert.False(string.IsNullOrWhiteSpace(saved.Properties.Metadata["bing-oss-session"]));

        var put = Assert.Single(handler.Requests, request => request.Method == "PUT");
        Assert.Equal("unit-bucket", put.Bucket);
        Assert.Equal($"test/{scope.Key}/attachments/folder/object", put.Key);
        Assert.Equal(bytes, put.Body);
        Assert.Equal(Convert.ToBase64String(MD5.HashData(bytes)), put.Headers["Content-MD5"]);
        Assert.Equal("text/plain", put.Headers["Content-Type"]);
        Assert.Equal("business-session", put.Headers["x-oss-meta-bing-session"]);
        Assert.False(string.IsNullOrWhiteSpace(put.Headers["x-oss-meta-bing-oss-session"]));

        using var read = new MemoryStream();
        await storage.ReadAsync(address, (stream, _) => stream.CopyToAsync(read));
        Assert.Equal(bytes, read.ToArray());

        using var ranged = new MemoryStream();
        await ((IFileRangeReader)storage).ReadRangeAsync(address, 1, 4,
            (stream, _) => stream.CopyToAsync(ranged));
        Assert.Equal(bytes.Skip(1).Take(4), ranged.ToArray());
        var rangeRequest = Assert.Single(handler.Requests, request => request.Headers.ContainsKey("Range"));
        Assert.Equal("bytes=1-4", rangeRequest.Headers["Range"]);
        Assert.Equal("standard", rangeRequest.Headers["x-oss-range-behavior"]);

        var properties = await storage.GetPropertiesAsync(address);
        Assert.Equal(bytes.Length, properties!.Length);
        await storage.DeleteAsync(address);
        Assert.Null(await storage.GetPropertiesAsync(address));
    }

    /// <summary>
    /// 验证 OSS 分片使用服务端回执及排他游标定位目标分片。
    /// </summary>
    [Fact]
    public async Task Multipart_UsesServerReceiptsAndExclusiveMarkerForPointQuery()
    {
        using var handler = new FakeOssHandler();
        using var client = FakeOssHandler.CreateClient(handler);
        var storage = Backend(client).Bind(StorageScope.Host);
        var multipart = (IMultipartFileStorage)storage;
        var address = new FileAddress("attachments", "multipart/object");
        var bytes = new byte[PartSize + 3];
        RandomNumberGenerator.Fill(bytes);
        var request = new FileWriteRequest(address, bytes.Length, FileWriteMode.Overwrite,
            metadata: new Dictionary<string, string> { ["source"] = "multipart-test", ["bing-session"] = "business-multipart" });

        var upload = await multipart.CreateMultipartAsync(request, PartSize);
        Assert.True(upload.Request.Metadata.ContainsKey("bing-upload-session"));
        Assert.Equal("business-multipart", upload.Request.Metadata["bing-session"]);
        Assert.NotEqual(FileStorageCapabilities.None, storage.Capabilities & FileStorageCapabilities.ServerVerifiedParts);
        Assert.Equal(FileStorageCapabilities.None, storage.Capabilities &
            (FileStorageCapabilities.CreateOnly | FileStorageCapabilities.SignedParts));

        using var firstSource = new MemoryStream(bytes, 0, PartSize, writable: false);
        var first = await multipart.UploadPartAsync(upload, 1, firstSource,
            Convert.ToBase64String(SHA256.HashData(bytes.AsSpan(0, PartSize))));
        using var lastSource = new NonSeekReadStream(bytes[^3..]);
        var last = await multipart.UploadPartAsync(upload, 2, lastSource);
        Assert.False(lastSource.IsDisposed);
        Assert.Equal(new[] { 1, 2 }, new[] { first.Number, last.Number });
        Assert.Equal(PartSize, first.Length);
        Assert.Equal(3, last.Length);
        Assert.NotEqual(Convert.ToBase64String(SHA256.HashData(bytes.AsSpan(0, PartSize))), first.ETag);

        var listed = await multipart.ListPartsAsync(upload);
        Assert.Equal(new[] { 1, 2 }, listed.Select(part => part.Number));
        Assert.Null(listed[0].Sha256);
        var point = await ((IMultipartFilePartReader)storage).GetPartAsync(upload, 2);
        Assert.Equal(2, point!.Number);
        var pointRequest = handler.Requests.Last(request => request.Method == "GET" && request.Query.ContainsKey("uploadId"));
        Assert.Equal("1", pointRequest.Query["part-number-marker"]);
        Assert.Equal("1", pointRequest.Query["max-parts"]);

        var completed = await multipart.CompleteMultipartAsync(upload, new[] { first, last });
        Assert.Equal(bytes.Length, completed.Properties.Length);
        Assert.Equal("multipart-test", completed.Properties.Metadata["source"]);
        Assert.Equal("business-multipart", completed.Properties.Metadata["bing-session"]);
        Assert.False(string.IsNullOrWhiteSpace(completed.Properties.Metadata["bing-oss-session"]));
        Assert.Contains(handler.Requests, request => request.Method == "POST" && request.Query.ContainsKey("uploadId"));
        Assert.All(handler.Requests.Where(request => request.Method == "PUT" && request.Query.ContainsKey("partNumber")),
            request => Assert.Equal(Convert.ToBase64String(MD5.HashData(request.Body)), request.Headers["Content-MD5"]));
    }

    /// <summary>
    /// 验证 OSS 分片拒绝错误 SHA256 或 CRC64 校验值。
    /// </summary>
    [Fact]
    public async Task UploadPart_RejectsWrongSha256AndCrc64Mismatch()
    {
        using var handler = new FakeOssHandler();
        using var client = FakeOssHandler.CreateClient(handler);
        var storage = Backend(client).Bind(StorageScope.Host);
        var multipart = (IMultipartFileStorage)storage;
        var upload = await multipart.CreateMultipartAsync(Request(PartSize), PartSize);
        var bytes = new byte[PartSize];
        var priorCount = handler.Requests.Count;

        using (var wrongDigestSource = new MemoryStream(bytes, writable: false))
        {
            var error = await Assert.ThrowsAsync<FileStorageException>(() => multipart.UploadPartAsync(upload, 1,
                wrongDigestSource, Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("wrong")))));
            Assert.Equal(FileStorageError.InvalidChecksum, error.Error);
        }
        Assert.Equal(priorCount, handler.Requests.Count);

        handler.ReturnInvalidUploadCrc = true;
        using var badCrcSource = new MemoryStream(bytes, writable: false);
        var crcError = await Assert.ThrowsAsync<FileStorageException>(() => multipart.UploadPartAsync(upload, 1, badCrcSource));
        Assert.Equal(FileStorageError.InvalidChecksum, crcError.Error);
    }

    /// <summary>
    /// 验证空文件通过对象上传保存且输入流不被释放。
    /// </summary>
    [Fact]
    public async Task EmptySave_UsesObjectPutWithEmptyMd5AndPreservesInputStream()
    {
        using var handler = new FakeOssHandler();
        using var client = FakeOssHandler.CreateClient(handler);
        var storage = Backend(client).Bind(StorageScope.Host);
        var source = new NonSeekReadStream(Array.Empty<byte>());

        var saved = await storage.SaveAsync(new FileWriteRequest(new FileAddress("attachments", "empty/object"), 0,
            FileWriteMode.Overwrite), source);

        Assert.Equal(0, saved.Properties.Length);
        Assert.False(source.IsDisposed);
        var put = Assert.Single(handler.Requests, request => request.Method == "PUT");
        Assert.Empty(put.Body);
        Assert.Equal(Convert.ToBase64String(MD5.HashData(Array.Empty<byte>())), put.Headers["Content-MD5"]);
    }

    /// <summary>
    /// 验证预先取消的保存不会发送请求或释放输入流。
    /// </summary>
    [Fact]
    public async Task PreCancelledSaveDoesNotSendRequestOrDisposeInput()
    {
        using var handler = new FakeOssHandler();
        using var client = FakeOssHandler.CreateClient(handler);
        var storage = Backend(client).Bind(StorageScope.Host);
        var source = new NonSeekReadStream(Array.Empty<byte>());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storage.SaveAsync(
            new FileWriteRequest(new FileAddress("attachments", "cancel/object"), 0, FileWriteMode.Overwrite),
            source, cancellation.Token));

        Assert.Empty(handler.Requests);
        Assert.False(source.IsDisposed);
    }

    /// <summary>
    /// 验证中止不存在的上传仍保持幂等。
    /// </summary>
    [Fact]
    public async Task AbortMissingUploadIsIdempotent()
    {
        using var handler = new FakeOssHandler();
        using var client = FakeOssHandler.CreateClient(handler);
        var storage = Backend(client).Bind(StorageScope.Host);
        var request = FileWriteRequest.RestoreMultipart(new FileAddress("attachments", "multipart/request"), 1,
            FileWriteMode.Overwrite, null,
            new Dictionary<string, string> { ["bing-upload-session"] = Guid.NewGuid().ToString("N") });
        var upload = new MultipartUpload("missing-upload", request, PartSize);
        var multipart = (IMultipartFileStorage)storage;

        await multipart.AbortMultipartAsync(upload);

        Assert.Single(handler.Requests, request => request.Method == "DELETE" && request.Query.ContainsKey("uploadId"));
    }

    /// <summary>
    /// 验证不可定位流长度不匹配时在发送请求前失败。
    /// </summary>
    /// <param name="actualLength">实际读取或写入的字节数。</param>
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public async Task Save_RejectsShortOrLongNonSeekableStreamsBeforeSendingRequest(int actualLength)
    {
        using var handler = new FakeOssHandler();
        using var client = FakeOssHandler.CreateClient(handler);
        var storage = Backend(client).Bind(StorageScope.Host);
        var source = new NonSeekReadStream(new byte[actualLength]);

        var error = await Assert.ThrowsAsync<FileStorageException>(() => storage.SaveAsync(
            new FileWriteRequest(new FileAddress("attachments", "invalid/length"), 3, FileWriteMode.Overwrite), source));

        Assert.Equal(FileStorageError.InvalidLength, error.Error);
        Assert.Empty(handler.Requests);
        Assert.False(source.IsDisposed);
    }

    /// <summary>
    /// 验证对象缺失返回空属性且读取与桶错误分别映射。
    /// </summary>
    [Fact]
    public async Task MissingObjectReturnsNullButReadAndBucketErrorsRemainDistinct()
    {
        using var handler = new FakeOssHandler();
        using var client = FakeOssHandler.CreateClient(handler);
        var storage = Backend(client).Bind(StorageScope.Host);
        var address = new FileAddress("attachments", "missing/object");

        Assert.Null(await storage.GetPropertiesAsync(address));
        var missing = await Assert.ThrowsAsync<FileStorageException>(() => storage.ReadAsync(address, (_, _) => Task.CompletedTask));
        Assert.Equal(FileStorageError.NotFound, missing.Error);
        Assert.Equal("request-error", missing.RequestId);
    }

    /// <summary>
    /// 验证下载使用 SDK 签名且拒绝不支持的操作模式。
    /// </summary>
    [Fact]
    public async Task SignedDownloadUsesSdkPresignerAndUnsupportedModesAreRejected()
    {
        using var handler = new FakeOssHandler();
        using var client = FakeOssHandler.CreateClient(handler);
        var storage = Backend(client).Bind(StorageScope.Host);
        var address = new FileAddress("attachments", "signed/name");

        var signed = await ((ISignedFileRequestProvider)storage).SignDownloadAsync(address, TimeSpan.FromMinutes(5), "name file.txt");

        Assert.Equal("GET", signed.Method);
        Assert.Equal("oss.test", signed.Url.Host);
        Assert.Contains("response-content-disposition", signed.Url.Query);
        Assert.Empty(handler.Requests);
        var createOnly = await Assert.ThrowsAsync<FileStorageException>(() => storage.SaveAsync(
            new FileWriteRequest(address, 0, FileWriteMode.CreateOnly), new MemoryStream()));
        Assert.Equal(FileStorageError.UnsupportedCapability, createOnly.Error);
        var signedPart = await Assert.ThrowsAsync<FileStorageException>(() =>
            ((ISignedFileRequestProvider)storage).SignPartAsync(null!, 1, "", TimeSpan.FromMinutes(1)));
        Assert.Equal(FileStorageError.UnsupportedCapability, signedPart.Error);
    }

    /// <summary>
    /// 验证存储实例复制桶映射且命名注册绑定指定范围。
    /// </summary>
    [Fact]
    public async Task BackendCopiesBucketMappingAndNamedRegistrationBindsScope()
    {
        using var handler = new FakeOssHandler();
        using var client = FakeOssHandler.CreateClient(handler);
        var options = Options();
        options.ContainerBuckets["attachments"] = "original-bucket";
        var backend = new AliyunOssFileStorageBackend("aliyun-oss", options, client);
        options.ContainerBuckets["attachments"] = "changed-bucket";

        var storage = backend.Bind(StorageScope.ForTenant("tenant-a"));
        await storage.GetPropertiesAsync(new FileAddress("attachments", "snapshot/key"));

        Assert.Equal("original-bucket", Assert.Single(handler.Requests).Bucket);
        Assert.NotEqual(FileStorageCapabilities.None, storage.Capabilities & FileStorageCapabilities.Range);

        var services = new ServiceCollection();
        services.AddBingFileStorage(configuration => configuration.DefaultStore = "aliyun-oss");
        services.AddBingAliyunOssStorage("aliyun-oss", configure =>
        {
            configure.Endpoint = "http://oss.test";
            configure.UsePathStyle = true;
            configure.ContainerBuckets["attachments"] = "unit-bucket";
        });
        using var provider = services.BuildServiceProvider();
        var registered = provider.GetRequiredService<IFileStorageFactory>().GetStorage("aliyun-oss", StorageScope.Host);
        Assert.Equal("aliyun-oss", registered.Name);
        Assert.Equal(StorageScope.Host, registered.Scope);
    }

    /// <summary>
    /// 创建 OSS 协议测试后端。
    /// </summary>
    /// <param name="client">执行测试请求的客户端。</param>
    /// <returns>使用指定客户端的 OSS 测试后端。</returns>
    private static AliyunOssFileStorageBackend Backend(AlibabaCloud.OSS.V2.Client client) =>
        new("aliyun-oss", Options(), client);

    /// <summary>
    /// 创建 OSS 协议测试选项。
    /// </summary>
    /// <returns>包含测试容器映射及分页设置的 OSS 选项。</returns>
    private static BingAliyunOssStorageOptions Options() => new()
    {
        StorageNamespace = "test",
        ListPartsPageSize = 1000,
        ContainerBuckets = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["attachments"] = "unit-bucket"
        }
    };

    /// <summary>
    /// 创建指定长度的覆盖写入请求。
    /// </summary>
    /// <param name="length">内容长度，单位为字节。</param>
    /// <returns>指定长度的覆盖写入请求。</returns>
    private static FileWriteRequest Request(long length) => new(new FileAddress("attachments", "multipart/request"),
        length, FileWriteMode.Overwrite);

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
        /// <summary>
        /// 获取当前资源是否已经释放。
        /// </summary>
        public bool IsDisposed { get; private set; }
        /// <inheritdoc />
        public override bool CanRead => !IsDisposed;
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
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _inner.ReadAsync(buffer, offset, count, cancellationToken);
        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);
        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            if (disposing)
                _inner.Dispose();
            base.Dispose(disposing);
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

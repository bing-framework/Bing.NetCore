using System.Net;
using System.Security.Cryptography;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.FileStorage.Qiniu.Tests;

public sealed class QiniuProviderTests
{
    internal static BingS3StorageOptions Options(S3UploadMode mode = S3UploadMode.ServerProxy) => new()
    {
        StorageNamespace = "tests", UploadMode = mode, ListPartsPageSize = 1,
        ContainerBuckets = new() { ["attachments"] = "qiniu-actual-bucket", ["public"] = "qiniu-actual-bucket" }
    };
    internal static IFileStorage Storage(RecordingS3Client client, S3UploadMode mode = S3UploadMode.ServerProxy) =>
        new S3FileStorageBackend("qiniu", Options(mode), client).Bind(StorageScope.Host);

    [Fact]
    public async Task Registration_DefaultsToProxy_AndSnapshotsBucketMappings()
    {
        var services = new ServiceCollection(); services.AddBingFileStorage(o => o.DefaultStore = "qiniu");
        BingQiniuStorageOptions captured = null!;
        services.AddBingQiniuStorage("qiniu", o =>
        {
            captured = o; o.Endpoint = "https://s3.cn-east-1.qiniucs.com"; o.Region = "cn-east-1";
            o.AccessKey = "test-key"; o.SecretKey = "test-secret"; o.ContainerBuckets["attachments"] = "qiniu-actual-bucket";
        });
        captured.ContainerBuckets["attachments"] = "changed-bucket";
        captured.UploadMode = S3UploadMode.SignedParts;
        using var servicesProvider = services.BuildServiceProvider();
        var storage = servicesProvider.GetRequiredService<IFileStorageFactory>().GetStorage("qiniu", StorageScope.Host);
        Assert.True(storage.Capabilities.HasFlag(FileStorageCapabilities.ServerVerifiedParts));
        Assert.False(storage.Capabilities.HasFlag(FileStorageCapabilities.SignedParts));
        var signed = await ((ISignedFileRequestProvider)storage).SignDownloadAsync(new("attachments", "file"), TimeSpan.FromMinutes(1), "中文.txt");
        Assert.Equal("/qiniu-actual-bucket/bing/h/attachments/file", signed.Url.AbsolutePath);
        Assert.Contains("response-content-disposition=", signed.Url.Query);
        Assert.Equal(FileStorageError.Configuration, (await Assert.ThrowsAsync<FileStorageException>(() => storage.GetPropertiesAsync(new("public", "file")))).Error);
    }

    [Theory]
    [InlineData("http://s3.cn-east-1.qiniucs.com", "cn-east-1", "key", true)]
    [InlineData("https://s3.cn-east-1.qiniucs.com", "cn-east-2", "key", true)]
    [InlineData("https://cdn.example.com", "cn-east-1", "key", true)]
    [InlineData("https://s3.cn-east-1.qiniucs.com", "cn-east-1", "", true)]
    [InlineData("https://s3.cn-east-1.qiniucs.com", "cn-east-1", "key", false)]
    public void Registration_RejectsInvalidConfiguration(string endpoint, string region, string key, bool mapping)
    {
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddBingQiniuStorage("qiniu", o =>
        { o.Endpoint = endpoint; o.Region = region; o.AccessKey = key; o.SecretKey = "secret"; if (mapping) o.ContainerBuckets["attachments"] = "actual-bucket"; }));
    }

    [Fact]
    public async Task Proxy_SaveReadRangeEmptyDelete_UseMappedBucketAndLogicalKeys()
    {
        using var client = new RecordingS3Client(); var storage = Storage(client);
        var bytes = new byte[] { 1, 3, 5, 7 }; var address = new FileAddress("attachments", "file");
        using var input = new NonSeekStream(bytes);
        var result = await storage.SaveAsync(new(address, bytes.Length, FileWriteMode.Overwrite), input);
        Assert.True(input.CanRead); Assert.Equal(bytes.Length, result.Properties.Length);
        Assert.Null(client.LastInitiate!.ChecksumAlgorithm);
        Assert.Null(client.LastPart!.ChecksumSHA256); Assert.False(client.LastPart.UseChunkEncoding);
        Assert.False(client.LastPart.DisablePayloadSigning); Assert.Null(client.LastComplete!.MpuObjectSize);
        using var output = new MemoryStream(); await storage.CopyToAsync(address, output); Assert.Equal(bytes, output.ToArray());
        using var range = new MemoryStream();
        await ((IFileRangeReader)storage).ReadRangeAsync(address, 1, 2, (s, ct) => s.CopyToAsync(range, ct));
        Assert.Equal(new byte[] { 3, 5 }, range.ToArray());
        var publicAddress = new FileAddress("public", "file");
        await storage.SaveAsync(new(publicAddress, 0, FileWriteMode.Overwrite), Stream.Null);
        Assert.Equal(0, (await storage.GetPropertiesAsync(publicAddress))!.Length);
        Assert.Equal(4, (await storage.GetPropertiesAsync(address))!.Length);
        await storage.DeleteAsync(address); await storage.DeleteAsync(address);
        Assert.Null(await storage.GetPropertiesAsync(address));
        Assert.All(client.Calls, call => Assert.Equal("qiniu-actual-bucket", call.Bucket));
        Assert.Contains(client.Calls, c => c.Key == "tests/h/public/file");
        Assert.Contains(client.Calls, c => c.Key == "tests/h/attachments/file");
    }

    [Fact]
    public async Task Proxy_MultipartPaginates_ValidatesReceipts_RecoversLostCompletion()
    {
        using var client = new RecordingS3Client(); var storage = Storage(client); var multipart = (IMultipartFileStorage)storage;
        const int size = 5 * 1024 * 1024;
        var upload = await multipart.CreateMultipartAsync(new(new("attachments", "parts"), size + 3, FileWriteMode.Overwrite), size);
        var first = await multipart.UploadPartAsync(upload, 1, new MemoryStream(new byte[size]));
        var second = await multipart.UploadPartAsync(upload, 2, new MemoryStream(new byte[3]));
        var actual = await multipart.ListPartsAsync(upload);
        Assert.Equal(2, actual.Count); Assert.All(actual, p => Assert.Null(p.Sha256)); Assert.Equal(2, client.ListCalls);
        Assert.Equal(FileStorageError.InvalidChecksum, (await Assert.ThrowsAsync<FileStorageException>(() => multipart.CompleteMultipartAsync(upload, actual))).Error);
        var wrong = new FilePart { Number = 2, Length = 3, ETag = "wrong", Sha256 = second.Sha256 };
        Assert.Equal(FileStorageError.InvalidPart, (await Assert.ThrowsAsync<FileStorageException>(() => multipart.CompleteMultipartAsync(upload, new[] { first, wrong }))).Error);
        client.LoseCompleteResponse = true;
        Assert.Equal(size + 3, (await multipart.CompleteMultipartAsync(upload, new[] { first, second })).Properties.Length);
        Assert.Equal(size + 3, (await multipart.CompleteMultipartAsync(upload, new[] { first, second })).Properties.Length);
        await multipart.AbortMultipartAsync(upload);
        Assert.All(client.Calls, c => Assert.Equal("qiniu-actual-bucket", c.Bucket));
    }

    [Fact]
    public async Task Proxy_RejectsWrongLengthChecksumCancellationAndSigning()
    {
        using var client = new RecordingS3Client(); var storage = Storage(client); var multipart = (IMultipartFileStorage)storage;
        var upload = await multipart.CreateMultipartAsync(new(new("attachments", "invalid"), 3, FileWriteMode.Overwrite), 5 * 1024 * 1024);
        Assert.Equal(FileStorageError.InvalidLength, (await Assert.ThrowsAsync<FileStorageException>(() => multipart.UploadPartAsync(upload, 1, new NonSeekStream(new byte[4])))).Error);
        Assert.Equal(FileStorageError.InvalidChecksum, (await Assert.ThrowsAsync<FileStorageException>(() => multipart.UploadPartAsync(upload, 1, new MemoryStream(new byte[3]), Convert.ToBase64String(new byte[32])))).Error);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => multipart.UploadPartAsync(upload, 1, new MemoryStream(new byte[3]), cancellationToken: new CancellationToken(true)));
        Assert.Equal(FileStorageError.UnsupportedCapability, (await Assert.ThrowsAsync<FileStorageException>(() => ((ISignedFileRequestProvider)storage).SignPartAsync(upload, 1, Convert.ToBase64String(new byte[32]), TimeSpan.FromMinutes(1)))).Error);
        Assert.Empty(client.Uploads[upload.UploadId].Parts);
        await multipart.AbortMultipartAsync(upload); Assert.Empty(client.Uploads);
        client.DenyAccess = true;
        Assert.Equal(FileStorageError.AccessDenied, (await Assert.ThrowsAsync<FileStorageException>(() => storage.GetPropertiesAsync(new("attachments", "file")))).Error);
    }

    [Fact]
    public async Task SignedMode_RetainsRemoteChecksum_AndRejectsModeSwitch()
    {
        using var client = new RecordingS3Client { OmitRemoteChecksum = false };
        var direct = Storage(client, S3UploadMode.SignedParts); var multipart = (IMultipartFileStorage)direct;
        var upload = await multipart.CreateMultipartAsync(new(new("attachments", "direct"), 3, FileWriteMode.Overwrite), 5 * 1024 * 1024);
        Assert.Equal("SignedParts", upload.Request.Metadata["bing-upload-mode"]);
        var receipt = await multipart.UploadPartAsync(upload, 1, new MemoryStream(new byte[3]));
        Assert.Equal(receipt.Sha256, client.LastPart!.ChecksumSHA256);
        var signed = await ((ISignedFileRequestProvider)direct).SignPartAsync(upload, 1, receipt.Sha256!, TimeSpan.FromMinutes(1));
        Assert.StartsWith("/qiniu-actual-bucket/", signed.Url.AbsolutePath); Assert.Contains("uploadId=", signed.Url.Query);
        Assert.Equal(receipt.Sha256, signed.Headers["x-amz-checksum-sha256"]);
        Assert.Equal(FileStorageError.Conflict, (await Assert.ThrowsAsync<FileStorageException>(() => ((IMultipartFileStorage)Storage(client)).ListPartsAsync(upload))).Error);
        client.OmitRemoteChecksum = true;
        Assert.Equal(FileStorageError.InvalidChecksum, (await Assert.ThrowsAsync<FileStorageException>(() => multipart.CompleteMultipartAsync(upload, new[] { receipt }))).Error);
    }

    [Fact]
    public async Task SharedBucket_PreservesTenantIsolation()
    {
        using var client = new RecordingS3Client();
        using var backend = new S3FileStorageBackend("qiniu", Options(), client);
        var a = backend.Bind(StorageScope.ForTenant("a")); var b = backend.Bind(StorageScope.ForTenant("b"));
        var address = new FileAddress("attachments", "same-key");
        await a.SaveAsync(new(address, 1, FileWriteMode.Overwrite), new MemoryStream(new byte[] { 1 }));
        await b.SaveAsync(new(address, 1, FileWriteMode.Overwrite), new MemoryStream(new byte[] { 2 }));
        using var first = new MemoryStream(); using var second = new MemoryStream();
        await a.CopyToAsync(address, first); await b.CopyToAsync(address, second);
        Assert.Equal(new byte[] { 1 }, first.ToArray()); Assert.Equal(new byte[] { 2 }, second.ToArray());
        Assert.Equal(2, client.Objects.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealSdk_ProxyUpload_SendsSignedPayloadWithoutChunkedOrOptionalChecksum(bool empty)
    {
        var capture = new CaptureHttpFactory();
        using var client = new AmazonS3Client(new BasicAWSCredentials("key", "secret"), new AmazonS3Config
        {
            ServiceURL = "https://s3.cn-east-1.qiniucs.com", AuthenticationRegion = "cn-east-1", ForcePathStyle = true,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED, HttpClientFactory = capture
        });
        var storage = new S3FileStorageBackend("qiniu", Options(), client).Bind(StorageScope.Host);
        var bytes = empty ? Array.Empty<byte>() : new byte[] { 4, 5, 6 };
        using var input = new NonSeekStream(bytes);
        if (empty) await storage.SaveAsync(new(new("attachments", "wire"), 0, FileWriteMode.Overwrite), input);
        else
        {
            var upload = new MultipartUpload("test-upload", new(new("attachments", "wire"), bytes.Length, FileWriteMode.Overwrite,
                metadata: new Dictionary<string, string> { ["bing-upload-mode"] = "ServerProxy", ["bing-upload-token"] = new('a', 32) }), 5 * 1024 * 1024);
            await ((IMultipartFileStorage)storage).UploadPartAsync(upload, 1, input);
        }
        Assert.True(input.CanRead);
        Assert.Equal(bytes, capture.Body);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), capture.Headers["x-amz-content-sha256"]);
        Assert.StartsWith("AWS4-HMAC-SHA256 ", capture.Headers["Authorization"]);
        Assert.False(capture.Headers.ContainsKey("x-amz-checksum-sha256"));
        Assert.False(capture.Headers.ContainsKey("x-amz-trailer"));
        Assert.Equal(bytes.Length.ToString(), capture.Headers["Content-Length"]);
        Assert.Equal("/qiniu-actual-bucket/tests/h/attachments/wire", capture.Uri!.AbsolutePath);
    }

    private sealed class CaptureHttpFactory : HttpClientFactory
    {
        public byte[] Body = Array.Empty<byte>(); public Uri? Uri;
        public Dictionary<string, string> Headers = new(StringComparer.OrdinalIgnoreCase);
        public override HttpClient CreateHttpClient(IClientConfig config) => new(new Handler(this));
        private sealed class Handler : HttpMessageHandler
        {
            private readonly CaptureHttpFactory _owner;
            public Handler(CaptureHttpFactory owner) => _owner = owner;
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                if (request.Method == HttpMethod.Head)
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Array.Empty<byte>()) };
                _owner.Uri = request.RequestUri;
                _owner.Body = await request.Content!.ReadAsByteArrayAsync(ct);
                _owner.Headers = request.Headers.Concat(request.Content.Headers).ToDictionary(x => x.Key, x => string.Join(",", x.Value), StringComparer.OrdinalIgnoreCase);
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("") };
                response.Headers.ETag = new("\"part-etag\""); return response;
            }
        }
    }

    private sealed class NonSeekStream : MemoryStream
    {
        public NonSeekStream(byte[] bytes) : base(bytes) { }
        public override bool CanSeek => false;
    }
}

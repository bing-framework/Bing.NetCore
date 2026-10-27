using System.Net;
using System.Security.Cryptography;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.FileStorage.Qiniu.Tests;

public sealed class QiniuLiveAttribute : FactAttribute
{
    public QiniuLiveAttribute(bool signed = false)
    {
        var required = new[] { "ENDPOINT", "REGION", "BUCKET", "ACCESS_KEY", "SECRET_KEY" };
        if (required.Any(key => string.IsNullOrWhiteSpace(QiniuLiveTests.Env(key))))
            Skip = "未配置独立七牛测试空间，云端兼容性尚未验证。";
        else if (signed && QiniuLiveTests.Env("SIGNED_TESTS") != "1")
            Skip = "设置 BING_FILE_STORAGE_QINIU_SIGNED_TESTS=1 后验证强校验直传。";
    }
}

/// <summary>仅操作隔离随机键，不创建空间、不修改 CORS 或空间权限。</summary>
public sealed class QiniuLiveTests
{
    internal static string? Env(string key) => Environment.GetEnvironmentVariable("BING_FILE_STORAGE_QINIU_" + key);
    private static ServiceProvider Services(S3UploadMode mode)
    {
        var services = new ServiceCollection(); services.AddBingFileStorage(o => o.DefaultStore = "qiniu");
        services.AddBingQiniuStorage("qiniu", o =>
        {
            o.Endpoint = Env("ENDPOINT")!; o.Region = Env("REGION")!;
            o.AccessKey = Env("ACCESS_KEY")!; o.SecretKey = Env("SECRET_KEY")!;
            o.ContainerBuckets["attachments"] = Env("BUCKET")!; o.UploadMode = mode;
            o.StorageNamespace = "bing-tests"; o.ListPartsPageSize = 1;
        });
        return services.BuildServiceProvider();
    }

    [QiniuLive]
    public async Task Proxy_RealMultipartRangeSignedDownloadAndIdempotentCompletion()
    {
        using var services = Services(S3UploadMode.ServerProxy);
        var storage = services.GetRequiredService<IFileStorageFactory>().GetStorage("qiniu", StorageScope.Host);
        var multipart = (IMultipartFileStorage)storage;
        var address = new FileAddress("attachments", "proxy-" + Guid.NewGuid().ToString("N"));
        var emptyAddress = new FileAddress("attachments", address.Key + "-empty");
        const int size = 5 * 1024 * 1024;
        var firstBytes = new byte[size]; RandomNumberGenerator.Fill(firstBytes); var tail = new byte[] { 1, 2, 3 };
        var upload = await multipart.CreateMultipartAsync(new(address, size + 3, FileWriteMode.Overwrite,
            metadata: new Dictionary<string, string> { ["bing-session"] = address.Key }), size);
        try
        {
            var second = await multipart.UploadPartAsync(upload, 2, new MemoryStream(tail));
            Assert.Null(await multipart.GetPartAsync(upload, 1));
            Assert.Equal(second.ETag, (await multipart.GetPartAsync(upload, 2))!.ETag);
            var first = await multipart.UploadPartAsync(upload, 1, new MemoryStream(firstBytes));
            Assert.Equal(first.ETag, (await multipart.GetPartAsync(upload, 1))!.ETag);
            Assert.Equal(2, (await multipart.ListPartsAsync(upload)).Count);
            var result = await multipart.CompleteMultipartAsync(upload, new[] { first, second });
            Assert.Equal(size + 3, result.Properties.Length); Assert.Equal(address.Key, result.Properties.Metadata["bing-session"]);
            Assert.Equal(size + 3, (await multipart.CompleteMultipartAsync(upload, new[] { first, second })).Properties.Length);
            using var range = new MemoryStream();
            await ((IFileRangeReader)storage).ReadRangeAsync(address, size, 3, (s, ct) => s.CopyToAsync(range, ct)); Assert.Equal(tail, range.ToArray());
            var signed = await ((ISignedFileRequestProvider)storage).SignDownloadAsync(address, TimeSpan.FromMinutes(1), "七牛测试.bin");
            using var http = new HttpClient(); using var response = await http.GetAsync(signed.Url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var downloaded = await response.Content.ReadAsByteArrayAsync();
            Assert.Equal(firstBytes.Concat(tail), downloaded);
            await storage.SaveAsync(new(emptyAddress, 0, FileWriteMode.Overwrite), Stream.Null);
            Assert.Equal(0, (await storage.GetPropertiesAsync(emptyAddress))!.Length);
        }
        finally { await multipart.AbortMultipartAsync(upload); await storage.DeleteAsync(address); await storage.DeleteAsync(emptyAddress); }
        await storage.DeleteAsync(address); Assert.Null(await storage.GetPropertiesAsync(address));
    }

    [QiniuLive(true)]
    public async Task SignedParts_RejectsWrongBytes_ListsChecksum_AndExpires()
    {
        using var services = Services(S3UploadMode.SignedParts);
        var storage = services.GetRequiredService<IFileStorageFactory>().GetStorage("qiniu", StorageScope.Host);
        var multipart = (IMultipartFileStorage)storage; var signer = (ISignedFileRequestProvider)storage;
        var address = new FileAddress("attachments", "signed-" + Guid.NewGuid().ToString("N"));
        var bytes = new byte[] { 1, 2, 3 }; var hash = Convert.ToBase64String(SHA256.HashData(bytes));
        var upload = await multipart.CreateMultipartAsync(new(address, bytes.Length, FileWriteMode.Overwrite), 5 * 1024 * 1024);
        try
        {
            using var http = new HttpClient();
            var signed = await signer.SignPartAsync(upload, 1, hash, TimeSpan.FromMinutes(1));
            using (var wrong = await Put(http, signed, new byte[] { 4, 5, 6 })) Assert.False(wrong.IsSuccessStatusCode);
            using (var valid = await Put(http, signed, bytes)) Assert.True(valid.IsSuccessStatusCode);
            var parts = await multipart.ListPartsAsync(upload); Assert.Equal(hash, Assert.Single(parts).Sha256);
            Assert.Equal(hash, (await multipart.GetPartAsync(upload, 1))!.Sha256);
            var expired = await signer.SignPartAsync(upload, 1, hash, TimeSpan.FromSeconds(1));
            await Task.Delay(2200);
            using (var invalid = await Put(http, expired, bytes)) Assert.False(invalid.IsSuccessStatusCode);
            await multipart.CompleteMultipartAsync(upload, parts);
        }
        finally { await multipart.AbortMultipartAsync(upload); await storage.DeleteAsync(address); }
    }

    [QiniuLive]
    public async Task Proxy_SigV4PayloadTampering_IsRejectedAndUploadCanBeAborted()
    {
        var config = new AmazonS3Config { ServiceURL = Env("ENDPOINT"), AuthenticationRegion = Env("REGION"), ForcePathStyle = true,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED, HttpClientFactory = new TamperFactory() };
        using var client = new AmazonS3Client(new BasicAWSCredentials(Env("ACCESS_KEY"), Env("SECRET_KEY")), config);
        var storage = new S3FileStorageBackend("qiniu", new BingS3StorageOptions { UploadMode = S3UploadMode.ServerProxy,
            ContainerBuckets = new() { ["attachments"] = Env("BUCKET")! }, StorageNamespace = "bing-tests" }, client).Bind(StorageScope.Host);
        var multipart = (IMultipartFileStorage)storage;
        var address = new FileAddress("attachments", "tamper-" + Guid.NewGuid().ToString("N"));
        var upload = await multipart.CreateMultipartAsync(new(address, 3, FileWriteMode.Overwrite), 5 * 1024 * 1024);
        try
        {
            var error = await Assert.ThrowsAsync<FileStorageException>(() => multipart.UploadPartAsync(upload, 1, new MemoryStream(new byte[] { 1, 2, 3 })));
            Assert.Contains(error.Error, new[] { FileStorageError.InvalidChecksum, FileStorageError.AccessDenied });
            Assert.Empty(await multipart.ListPartsAsync(upload));
        }
        finally { await multipart.AbortMultipartAsync(upload); await storage.DeleteAsync(address); }
    }

    private static Task<HttpResponseMessage> Put(HttpClient http, SignedFileRequest signed, byte[] bytes)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.TryAddWithoutValidation("x-amz-checksum-sha256", signed.Headers["x-amz-checksum-sha256"]);
        return http.PutAsync(signed.Url, content);
    }
    private sealed class TamperFactory : HttpClientFactory
    {
        public override HttpClient CreateHttpClient(IClientConfig config) => new(new TamperHandler());
        private sealed class TamperHandler : DelegatingHandler
        {
            public TamperHandler() : base(new HttpClientHandler()) { }
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                if (request.Method == HttpMethod.Put && request.RequestUri!.Query.Contains("uploadId=", StringComparison.Ordinal))
                {
                    var original = request.Content!; var bytes = await original.ReadAsByteArrayAsync(ct); bytes[0] ^= 1;
                    var replacement = new ByteArrayContent(bytes);
                    foreach (var header in original.Headers) replacement.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    request.Content = replacement; original.Dispose();
                }
                return await base.SendAsync(request, ct);
            }
        }
    }
}

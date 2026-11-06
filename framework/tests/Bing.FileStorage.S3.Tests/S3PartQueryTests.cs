using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Bing.FileStorage;
using Xunit;

namespace Bing.FileStorage.S3.Tests;

/// <summary>
/// 覆盖并发获取同一锁时只能有一个持有者等测试场景。
/// </summary>
public sealed class S3PartQueryTests
{
    /// <summary>
    /// 定义 S3 分片查询测试使用的分片大小。
    /// </summary>
    private const int PartSize = 5 * 1024 * 1024;

    /// <summary>
    /// 验证分片点查使用排他游标且正确处理首尾及缺失分片。
    /// </summary>
    [Fact]
    public async Task PointQuery_UsesExclusiveMarkerAndReturnsFirstOrLastPart()
    {
        using var client = new RecordingS3Client(request =>
        {
            var marker = ParseMarker(request.PartNumberMarker);
            return Page(marker switch
            {
                0 => new[] { Detail(1, PartSize) },
                2 => new[] { Detail(3, 3) },
                1 => new[] { Detail(3, 3) },
                _ => Array.Empty<PartDetail>()
            });
        });
        var storage = CreateStorage(client, S3UploadMode.SignedParts);
        var multipart = (IMultipartFileStorage)storage;
        var upload = Upload(partCount: 3, S3UploadMode.SignedParts);

        var first = await multipart.GetPartAsync(upload, 1);
        var last = await multipart.GetPartAsync(upload, 3);
        var missing = await multipart.GetPartAsync(upload, 2);

        Assert.Equal(1, first!.Number);
        Assert.Equal(3, last!.Number);
        Assert.Null(missing);
        Assert.Equal(new[] { "0", "2", "1" }, client.Requests.Select(request => request.Marker));
        Assert.All(client.Requests, request => Assert.Equal(1, request.MaxParts));
    }

    /// <summary>
    /// 验证分片点查兼容两种持久化上传模式。
    /// </summary>
    /// <param name="mode">分片上传模式。</param>
    /// <param name="expectedCapability">预期的文件存储能力。</param>
    [Theory]
    [InlineData(S3UploadMode.SignedParts, FileStorageCapabilities.SignedParts)]
    [InlineData(S3UploadMode.ServerProxy, FileStorageCapabilities.ServerVerifiedParts)]
    public async Task PointQuery_WorksWithBothPersistedUploadModes(S3UploadMode mode, FileStorageCapabilities expectedCapability)
    {
        using var client = new RecordingS3Client(_ => Page(new[] { Detail(1, 3) }));
        var storage = CreateStorage(client, mode);

        var result = await ((IMultipartFileStorage)storage).GetPartAsync(Upload(1, mode), 1);

        Assert.Equal(1, result!.Number);
        Assert.NotEqual(FileStorageCapabilities.None, storage.Capabilities & expectedCapability);
    }

    /// <summary>
    /// 验证默认 S3 后端使用签名分片模式。
    /// </summary>
    [Fact]
    public async Task DefaultBackend_UsesSignedPartsMode()
    {
        using var client = new RecordingS3Client(_ => Page(new[] { Detail(1, 3) }));
        var storage = new S3FileStorageBackend("s3", "test", client).Bind(StorageScope.Host);

        var result = await ((IMultipartFileStorage)storage).GetPartAsync(Upload(1, S3UploadMode.SignedParts), 1);

        Assert.Equal(1, result!.Number);
        Assert.NotEqual(FileStorageCapabilities.None, storage.Capabilities & FileStorageCapabilities.SignedParts);
    }

    /// <summary>
    /// 验证分片点查使用映射桶与范围隔离的对象键。
    /// </summary>
    [Fact]
    public async Task PointQuery_UsesMappedBucketAndScopeSpecificObjectKey()
    {
        using var client = new RecordingS3Client(_ => Page(new[] { Detail(1, 3) }));
        var backend = new S3FileStorageBackend("s3", Options(S3UploadMode.ServerProxy), client);
        var address = new FileAddress("attachments", "tenant/object");
        var tenantA = (IMultipartFileStorage)backend.Bind(StorageScope.ForTenant("tenant-a"));
        var tenantB = (IMultipartFileStorage)backend.Bind(StorageScope.ForTenant("tenant-b"));

        await tenantA.GetPartAsync(Upload(1, S3UploadMode.ServerProxy, address), 1);
        await tenantB.GetPartAsync(Upload(1, S3UploadMode.ServerProxy, address), 1);

        Assert.Equal("actual-attachments-bucket", client.Requests[0].Bucket);
        Assert.Equal(client.Requests[0].Bucket, client.Requests[1].Bucket);
        Assert.NotEqual(client.Requests[0].Key, client.Requests[1].Key);
        Assert.All(client.Requests, request => Assert.Equal("upload", request.UploadId));
    }

    /// <summary>
    /// 验证分片点查保留拒绝访问错误而不误报缺失。
    /// </summary>
    [Fact]
    public async Task PointQuery_PropagatesAccessDeniedInsteadOfReturningMissing()
    {
        using var client = new RecordingS3Client(_ => throw new AmazonS3Exception("denied")
        {
            ErrorCode = "AccessDenied",
            StatusCode = HttpStatusCode.Forbidden
        });
        var storage = CreateStorage(client, S3UploadMode.ServerProxy);

        var error = await Assert.ThrowsAsync<FileStorageException>(() =>
            ((IMultipartFileStorage)storage).GetPartAsync(Upload(1, S3UploadMode.ServerProxy), 1));

        Assert.Equal(FileStorageError.AccessDenied, error.Error);
    }

    /// <summary>
    /// 验证分片点查在发送请求前响应取消。
    /// </summary>
    [Fact]
    public async Task PointQuery_HonorsCancellationBeforeSendingRequest()
    {
        using var client = new RecordingS3Client(_ => Page(new[] { Detail(1, 3) }));
        var storage = CreateStorage(client, S3UploadMode.ServerProxy);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ((IMultipartFileStorage)storage).GetPartAsync(Upload(1, S3UploadMode.ServerProxy), 1, cancellation.Token));

        Assert.Empty(client.Requests);
    }

    /// <summary>
    /// 验证完整分片列表拒绝结构异常的页面。
    /// </summary>
    /// <param name="kind">用于选择待测输入形态的场景标记。</param>
    /// <param name="expected">预期的断言结果。</param>
    [Theory]
    [InlineData("empty-truncated", FileStorageError.OutcomeUnknown)]
    [InlineData("wrong-cursor", FileStorageError.OutcomeUnknown)]
    [InlineData("missing-etag", FileStorageError.InvalidPart)]
    [InlineData("unordered", FileStorageError.InvalidPart)]
    [InlineData("negative-length", FileStorageError.InvalidPart)]
    [InlineData("too-many-parts", FileStorageError.InvalidPart)]
    public async Task FullListing_RejectsMalformedPages(string kind, FileStorageError expected)
    {
        using var client = new RecordingS3Client(_ => kind switch
        {
            "empty-truncated" => new ListPartsResponse { IsTruncated = true, NextPartNumberMarker = 1, Parts = new List<PartDetail>() },
            "wrong-cursor" => new ListPartsResponse { IsTruncated = true, NextPartNumberMarker = 2, Parts = new List<PartDetail> { Detail(1, 1) } },
            "missing-etag" => Page(new[] { new PartDetail { PartNumber = 1, Size = 1, ETag = " " } }),
            "unordered" => Page(new[] { Detail(2, 1), Detail(1, 1) }),
            "negative-length" => Page(new[] { Detail(1, -1) }),
            "too-many-parts" => Page(new[] { Detail(1, 1), Detail(2, 1), Detail(3, 1) }),
            _ => throw new InvalidOperationException(kind)
        });
        var storage = CreateStorage(client, S3UploadMode.SignedParts, pageSize: kind == "too-many-parts" ? 2 : 10);

        var error = await Assert.ThrowsAsync<FileStorageException>(() =>
            ((IMultipartFileStorage)storage).ListPartsAsync(Upload(3, S3UploadMode.SignedParts)));

        Assert.Equal(expected, error.Error);
    }

    /// <summary>
    /// 验证完整分片列表按经校验的多页游标继续读取。
    /// </summary>
    [Fact]
    public async Task FullListing_UsesValidatedMultiPageCursor()
    {
        using var client = new RecordingS3Client(request =>
        {
            var marker = ParseMarker(request.PartNumberMarker);
            return marker switch
            {
                0 => new ListPartsResponse
                {
                    IsTruncated = true,
                    NextPartNumberMarker = 2,
                    Parts = new List<PartDetail> { Detail(1, PartSize), Detail(2, PartSize) }
                },
                2 => Page(new[] { Detail(3, 3) }),
                _ => throw new InvalidOperationException("Unexpected marker: " + marker)
            };
        });
        var storage = CreateStorage(client, S3UploadMode.SignedParts, pageSize: 2);

        var parts = await ((IMultipartFileStorage)storage).ListPartsAsync(Upload(3, S3UploadMode.SignedParts));

        Assert.Equal(new[] { 1, 2, 3 }, parts.Select(part => part.Number));
        Assert.Equal(new string?[] { null, "2" }, client.Requests.Select(request => request.Marker));
        Assert.All(client.Requests, request => Assert.Equal(2, request.MaxParts));
    }

    /// <summary>
    /// 验证大量分片中的目标点查只发送一次请求并返回一条记录。
    /// </summary>
    [Fact]
    public async Task MetadataOnlyProbe_Queries6400PartsWithOneRequestAndOneReturnedPart()
    {
        const int partCount = 6400;
        using var client = new RecordingS3Client(request =>
        {
            var marker = ParseMarker(request.PartNumberMarker);
            var remaining = Enumerable.Range(marker + 1, partCount - marker);
            var selected = remaining.Take(request.MaxParts ?? 1000).ToArray();
            var truncated = selected.Length < partCount - marker;
            return new ListPartsResponse
            {
                IsTruncated = truncated,
                NextPartNumberMarker = truncated ? selected[^1] : marker,
                Parts = selected.Select(number => Detail(number, number == partCount ? 3 : PartSize)).ToList()
            };
        });
        var storage = CreateStorage(client, S3UploadMode.ServerProxy, pageSize: 1000);
        var upload = Upload(partCount, S3UploadMode.ServerProxy);

        var full = await ((IMultipartFileStorage)storage).ListPartsAsync(upload);
        var fullRequestCount = client.Requests.Count;
        var fullReturnedCount = client.ReturnedPartCount;
        client.ResetCounts();

        var last = await ((IMultipartFileStorage)storage).GetPartAsync(upload, partCount);

        Assert.Equal(partCount, full.Count);
        Assert.Equal(7, fullRequestCount);
        Assert.Equal(partCount, fullReturnedCount);
        Assert.Equal(partCount, last!.Number);
        Assert.Single(client.Requests);
        Assert.Equal(partCount - 1, ParseMarker(client.Requests[0].Marker));
        Assert.Equal(1, client.Requests[0].MaxParts);
        Assert.Equal(1, client.ReturnedPartCount);
    }

    /// <summary>
    /// 创建指定作用域及上传模式的 S3 测试存储。
    /// </summary>
    /// <param name="client">执行测试请求的客户端。</param>
    /// <param name="mode">分片上传模式。</param>
    /// <param name="scope">文件存储作用域；未指定时使用宿主作用域。</param>
    /// <param name="pageSize">分片列表的分页大小。</param>
    /// <returns>已绑定目标作用域的测试文件存储。</returns>
    private static IFileStorage CreateStorage(RecordingS3Client client, S3UploadMode mode, StorageScope? scope = null, int pageSize = 1000) =>
        new S3FileStorageBackend("s3", Options(mode, pageSize), client).Bind(scope ?? StorageScope.Host);

    /// <summary>
    /// 创建指定上传模式及分页大小的 S3 选项。
    /// </summary>
    /// <param name="mode">分片上传模式。</param>
    /// <param name="pageSize">分片列表的分页大小。</param>
    /// <returns>包含上传模式、容器映射及分页设置的选项。</returns>
    private static BingS3StorageOptions Options(S3UploadMode mode, int pageSize = 1000) => new()
    {
        StorageNamespace = "test",
        ListPartsPageSize = pageSize,
        UploadMode = mode,
        ContainerBuckets = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["attachments"] = "actual-attachments-bucket"
        }
    };

    /// <summary>
    /// 创建指定分片数的上传测试会话。
    /// </summary>
    /// <param name="partCount">测试上传的分片数量。</param>
    /// <param name="mode">分片上传模式。</param>
    /// <param name="address">目标文件地址；未指定时使用测试地址。</param>
    /// <returns>包含写入请求及分片设置的测试上传会话。</returns>
    private static MultipartUpload Upload(int partCount, S3UploadMode mode, FileAddress? address = null)
    {
        var length = partCount == 0 ? 0 : (long)PartSize * (partCount - 1) + 3;
        var metadata = new Dictionary<string, string> { ["bing-upload-mode"] = mode.ToString() };
        var request = FileWriteRequest.RestoreMultipart(address ?? new FileAddress("attachments", "tests/object"),
            length, FileWriteMode.CreateOnly, "application/octet-stream", metadata);
        return new MultipartUpload("upload", request, PartSize);
    }

    /// <summary>
    /// 解析分片分页标记。
    /// </summary>
    /// <param name="marker">分片分页标记；空值表示初始页。</param>
    /// <returns>解析得到的分页序号；标记为空或无效时为零。</returns>
    private static int ParseMarker(string? marker) => int.TryParse(marker, out var value) ? value : 0;

    /// <summary>
    /// 创建指定编号及长度的分片详情。
    /// </summary>
    /// <param name="number">文件分片编号。</param>
    /// <param name="length">内容长度，单位为字节。</param>
    /// <returns>包含编号、长度及实体标签的分片详情。</returns>
    private static PartDetail Detail(int number, long length) => new()
    {
        PartNumber = number,
        Size = length,
        ETag = "etag-" + number
    };

    /// <summary>
    /// 创建完整的分片列表响应。
    /// </summary>
    /// <param name="parts">需要返回的分片详情集合。</param>
    /// <returns>包含全部给定分片且未截断的列表响应。</returns>
    private static ListPartsResponse Page(IEnumerable<PartDetail> parts) => new()
    {
        IsTruncated = false,
        Parts = parts.ToList()
    };

    /// <summary>
    /// 为S3PartQueryTests测试记录并观察相关调用状态。
    /// </summary>
    private sealed class RecordingS3Client : AmazonS3Client
    {
        /// <summary>
        /// 保存模拟 S3 客户端处理列表请求的委托。
        /// </summary>
        private readonly Func<ListPartsRequest, ListPartsResponse> _handler;

        /// <summary>
        /// 初始化一个 <see cref="RecordingS3Client" /> 类型的实例。
        /// </summary>
        /// <param name="handler">用于处理相应请求或事件的处理器。</param>
        public RecordingS3Client(Func<ListPartsRequest, ListPartsResponse> handler)
            : base(new BasicAWSCredentials("test-access", "test-secret"), new AmazonS3Config
            {
                ServiceURL = "http://127.0.0.1:19000",
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1",
                UseHttp = true
            }) => _handler = handler;

        /// <summary>
        /// 获取已记录的请求列表。
        /// </summary>
        public List<ListPartsCall> Requests { get; } = new();
        /// <summary>
        /// 获取分片查询实际返回的数量。
        /// </summary>
        public int ReturnedPartCount { get; private set; }

        /// <inheritdoc />
        public override Task<ListPartsResponse> ListPartsAsync(ListPartsRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(new ListPartsCall(request.BucketName, request.Key, request.UploadId,
                request.PartNumberMarker, request.MaxParts));
            var response = _handler(request);
            ReturnedPartCount += response.Parts?.Count ?? 0;
            return Task.FromResult(response);
        }

        /// <summary>
        /// 清空 S3 请求记录及返回分片计数。
        /// </summary>
        public void ResetCounts()
        {
            Requests.Clear();
            ReturnedPartCount = 0;
        }
    }

    /// <summary>
    /// 记录分片列表请求使用的存储桶、对象和分页参数。
    /// </summary>
    /// <param name="Bucket">存储桶名称。</param>
    /// <param name="Key">租户键。</param>
    /// <param name="UploadId">分片上传标识。</param>
    /// <param name="Marker">分片分页标记。</param>
    /// <param name="MaxParts">本次最多返回的分片数。</param>
    private sealed record ListPartsCall(string Bucket, string Key, string UploadId, string? Marker, int? MaxParts);
}

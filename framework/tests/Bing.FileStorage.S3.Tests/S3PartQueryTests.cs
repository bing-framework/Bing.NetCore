using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Bing.FileStorage;
using Xunit;

namespace Bing.FileStorage.S3.Tests;

public sealed class S3PartQueryTests
{
    private const int PartSize = 5 * 1024 * 1024;

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

    [Fact]
    public async Task DefaultBackend_UsesSignedPartsMode()
    {
        using var client = new RecordingS3Client(_ => Page(new[] { Detail(1, 3) }));
        var storage = new S3FileStorageBackend("s3", "test", client).Bind(StorageScope.Host);

        var result = await ((IMultipartFileStorage)storage).GetPartAsync(Upload(1, S3UploadMode.SignedParts), 1);

        Assert.Equal(1, result!.Number);
        Assert.NotEqual(FileStorageCapabilities.None, storage.Capabilities & FileStorageCapabilities.SignedParts);
    }

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

    private static IFileStorage CreateStorage(RecordingS3Client client, S3UploadMode mode, StorageScope? scope = null, int pageSize = 1000) =>
        new S3FileStorageBackend("s3", Options(mode, pageSize), client).Bind(scope ?? StorageScope.Host);

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

    private static MultipartUpload Upload(int partCount, S3UploadMode mode, FileAddress? address = null)
    {
        var length = partCount == 0 ? 0 : (long)PartSize * (partCount - 1) + 3;
        var metadata = new Dictionary<string, string> { ["bing-upload-mode"] = mode.ToString() };
        var request = FileWriteRequest.RestoreMultipart(address ?? new FileAddress("attachments", "tests/object"),
            length, FileWriteMode.CreateOnly, "application/octet-stream", metadata);
        return new MultipartUpload("upload", request, PartSize);
    }

    private static int ParseMarker(string? marker) => int.TryParse(marker, out var value) ? value : 0;

    private static PartDetail Detail(int number, long length) => new()
    {
        PartNumber = number,
        Size = length,
        ETag = "etag-" + number
    };

    private static ListPartsResponse Page(IEnumerable<PartDetail> parts) => new()
    {
        IsTruncated = false,
        Parts = parts.ToList()
    };

    private sealed class RecordingS3Client : AmazonS3Client
    {
        private readonly Func<ListPartsRequest, ListPartsResponse> _handler;

        public RecordingS3Client(Func<ListPartsRequest, ListPartsResponse> handler)
            : base(new BasicAWSCredentials("test-access", "test-secret"), new AmazonS3Config
            {
                ServiceURL = "http://127.0.0.1:19000",
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1",
                UseHttp = true
            }) => _handler = handler;

        public List<ListPartsCall> Requests { get; } = new();
        public int ReturnedPartCount { get; private set; }

        public override Task<ListPartsResponse> ListPartsAsync(ListPartsRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(new ListPartsCall(request.BucketName, request.Key, request.UploadId,
                request.PartNumberMarker, request.MaxParts));
            var response = _handler(request);
            ReturnedPartCount += response.Parts?.Count ?? 0;
            return Task.FromResult(response);
        }

        public void ResetCounts()
        {
            Requests.Clear();
            ReturnedPartCount = 0;
        }
    }

    private sealed record ListPartsCall(string Bucket, string Key, string UploadId, string? Marker, int? MaxParts);
}

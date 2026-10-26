using System.Net;
using System.Security.Cryptography;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace Bing.FileStorage.Qiniu.Tests;

/// <summary>仅用于本地协议契约与数据库故障注入；不构成七牛云端验证证据。</summary>
public sealed class RecordingS3Client : AmazonS3Client
{
    public RecordingS3Client() : base(new BasicAWSCredentials("test-key", "test-secret"), new AmazonS3Config
    { ServiceURL = "https://s3.cn-east-1.qiniucs.com", AuthenticationRegion = "cn-east-1", ForcePathStyle = true }) { }
    public readonly Dictionary<string, Upload> Uploads = new();
    public readonly Dictionary<string, StoredObject> Objects = new();
    public readonly List<(string Operation, string Bucket, string Key)> Calls = new();
    public UploadPartRequest? LastPart { get; private set; }
    public InitiateMultipartUploadRequest? LastInitiate { get; private set; }
    public CompleteMultipartUploadRequest? LastComplete { get; private set; }
    public Func<Task>? AfterUpload { get; set; }
    public Func<Task>? BeforeAbort { get; set; }
    public bool LoseCompleteResponse { get; set; }
    public bool OmitRemoteChecksum { get; set; } = true;
    public int ListCalls { get; private set; }
    public int AbortCalls { get; private set; }
    public bool DenyAccess { get; set; }
    public static string ObjectId(string bucket, string key) => bucket + "/" + key;
    private void Record(string op, string bucket, string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (DenyAccess) throw new AmazonS3Exception("denied") { ErrorCode = "AccessDenied", StatusCode = HttpStatusCode.Forbidden };
        Calls.Add((op, bucket, key));
    }
    private Upload Find(string id) => Uploads.TryGetValue(id, out var value) ? value : throw new AmazonS3Exception("gone")
    { ErrorCode = "NoSuchUpload", StatusCode = HttpStatusCode.NotFound };
    public override Task<InitiateMultipartUploadResponse> InitiateMultipartUploadAsync(InitiateMultipartUploadRequest request, CancellationToken ct = default)
    {
        Record("create", request.BucketName, request.Key, ct); LastInitiate = request;
        var id = Guid.NewGuid().ToString("N");
        Uploads[id] = new Upload { Bucket = request.BucketName, Key = request.Key,
            Metadata = request.Metadata.Keys.ToDictionary(x => x, x => request.Metadata[x]) };
        return Task.FromResult(new InitiateMultipartUploadResponse { UploadId = id });
    }
    public override async Task<UploadPartResponse> UploadPartAsync(UploadPartRequest request, CancellationToken ct = default)
    {
        Record("part", request.BucketName, request.Key, ct); LastPart = request;
        var upload = Find(request.UploadId);
        using var memory = new MemoryStream(); await request.InputStream.CopyToAsync(memory, ct);
        var bytes = memory.ToArray();
        var hash = Convert.ToBase64String(SHA256.HashData(bytes));
        var etag = "\"" + Guid.NewGuid().ToString("N") + "\"";
        upload.Parts[request.PartNumber!.Value] = new Part(bytes, etag, hash);
        if (AfterUpload != null) await AfterUpload();
        return new UploadPartResponse { PartNumber = request.PartNumber, ETag = etag, ChecksumSHA256 = OmitRemoteChecksum ? null : hash };
    }
    public override Task<ListPartsResponse> ListPartsAsync(ListPartsRequest request, CancellationToken ct = default)
    {
        Record("list", request.BucketName, request.Key, ct); ListCalls++;
        var upload = Find(request.UploadId);
        var marker = int.TryParse(request.PartNumberMarker, out var m) ? m : 0;
        var all = upload.Parts.Where(x => x.Key > marker).OrderBy(x => x.Key).ToArray();
        var page = all.Take(request.MaxParts ?? 1000).ToArray();
        return Task.FromResult(new ListPartsResponse { IsTruncated = page.Length < all.Length,
            NextPartNumberMarker = page.Length == 0 ? marker : page.Last().Key,
            Parts = page.Select(x => new PartDetail { PartNumber = x.Key, Size = x.Value.Bytes.Length,
                ETag = x.Value.ETag, ChecksumSHA256 = OmitRemoteChecksum ? null : x.Value.Hash }).ToList() });
    }
    public override Task<CompleteMultipartUploadResponse> CompleteMultipartUploadAsync(CompleteMultipartUploadRequest request, CancellationToken ct = default)
    {
        Record("complete", request.BucketName, request.Key, ct); LastComplete = request;
        var upload = Find(request.UploadId);
        var parts = request.PartETags.Select(p => upload.Parts[p.PartNumber!.Value]).ToArray();
        Objects[ObjectId(request.BucketName, request.Key)] = new StoredObject(parts.SelectMany(p => p.Bytes).ToArray(), upload.Metadata);
        Uploads.Remove(request.UploadId);
        if (LoseCompleteResponse) { LoseCompleteResponse = false; throw new HttpRequestException("lost complete response"); }
        return Task.FromResult(new CompleteMultipartUploadResponse());
    }
    public override async Task<AbortMultipartUploadResponse> AbortMultipartUploadAsync(AbortMultipartUploadRequest request, CancellationToken ct = default)
    {
        Record("abort", request.BucketName, request.Key, ct); AbortCalls++;
        if (BeforeAbort != null) await BeforeAbort();
        Uploads.Remove(request.UploadId);
        return new AbortMultipartUploadResponse();
    }
    public override Task<GetObjectMetadataResponse> GetObjectMetadataAsync(GetObjectMetadataRequest request, CancellationToken ct = default)
    {
        Record("head", request.BucketName, request.Key, ct);
        var obj = GetObject(request.BucketName, request.Key);
        var result = new GetObjectMetadataResponse { ContentLength = obj.Bytes.Length, ETag = "object-etag", ContentType = "application/octet-stream" };
        foreach (var pair in obj.Metadata) result.Metadata.Add(pair.Key, pair.Value);
        return Task.FromResult(result);
    }
    public override Task<GetObjectResponse> GetObjectAsync(GetObjectRequest request, CancellationToken ct = default)
    {
        Record("get", request.BucketName, request.Key, ct);
        var obj = GetObject(request.BucketName, request.Key); var bytes = obj.Bytes;
        if (request.ByteRange != null) bytes = bytes.Skip((int)request.ByteRange.Start).Take((int)(request.ByteRange.End - request.ByteRange.Start + 1)).ToArray();
        return Task.FromResult(new GetObjectResponse { ResponseStream = new MemoryStream(bytes), ContentLength = bytes.Length });
    }
    public override async Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request, CancellationToken ct = default)
    {
        Record("put", request.BucketName, request.Key, ct);
        using var memory = new MemoryStream(); await request.InputStream.CopyToAsync(memory, ct);
        Objects[ObjectId(request.BucketName, request.Key)] = new StoredObject(memory.ToArray(), request.Metadata.Keys.ToDictionary(x => x, x => request.Metadata[x]));
        return new PutObjectResponse();
    }
    public override Task<DeleteObjectResponse> DeleteObjectAsync(DeleteObjectRequest request, CancellationToken ct = default)
    {
        Record("delete", request.BucketName, request.Key, ct); Objects.Remove(ObjectId(request.BucketName, request.Key));
        return Task.FromResult(new DeleteObjectResponse());
    }
    private StoredObject GetObject(string bucket, string key) => Objects.TryGetValue(ObjectId(bucket, key), out var obj) ? obj :
        throw new AmazonS3Exception("missing") { ErrorCode = "NoSuchKey", StatusCode = HttpStatusCode.NotFound };
    public sealed class Upload
    {
        public string Bucket = "", Key = "";
        public Dictionary<string, string> Metadata = new();
        public Dictionary<int, Part> Parts = new();
    }
    public sealed record Part(byte[] Bytes, string ETag, string Hash);
    public sealed record StoredObject(byte[] Bytes, Dictionary<string, string> Metadata);
}

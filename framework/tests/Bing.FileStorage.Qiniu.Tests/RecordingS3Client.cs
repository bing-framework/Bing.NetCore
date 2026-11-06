using System.Net;
using System.Security.Cryptography;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace Bing.FileStorage.Qiniu.Tests;

/// <summary>
/// 记录 S3 协议与故障注入的本地测试客户端。
/// </summary>
/// <remarks>
/// 仅用于本地协议契约与数据库故障注入；不构成七牛云端验证证据。
/// </remarks>
public sealed class RecordingS3Client : AmazonS3Client
{
    /// <summary>
    /// 初始化一个 <see cref="RecordingS3Client" /> 类型的实例。
    /// </summary>
    public RecordingS3Client() : base(new BasicAWSCredentials("test-key", "test-secret"), new AmazonS3Config
    { ServiceURL = "https://s3.cn-east-1.qiniucs.com", AuthenticationRegion = "cn-east-1", ForcePathStyle = true }) { }
    /// <summary>
    /// 保存模拟 S3 客户端记录的上传。
    /// </summary>
    public readonly Dictionary<string, Upload> Uploads = new();
    /// <summary>
    /// 保存模拟 S3 客户端记录的对象。
    /// </summary>
    public readonly Dictionary<string, StoredObject> Objects = new();
    /// <summary>
    /// 保存模拟 S3 客户端记录的调用。
    /// </summary>
    public readonly List<(string Operation, string Bucket, string Key)> Calls = new();
    /// <summary>
    /// 保存模拟 S3 客户端记录的列表请求。
    /// </summary>
    public readonly List<(string? Marker, int? MaxParts)> ListRequests = new();
    /// <summary>
    /// 获取分片查询累计返回的分片数量。
    /// </summary>
    public int ListedParts { get; private set; }
    /// <summary>
    /// 获取最近一次上传的分片。
    /// </summary>
    public UploadPartRequest? LastPart { get; private set; }
    /// <summary>
    /// 获取最近一次初始化上传的请求。
    /// </summary>
    public InitiateMultipartUploadRequest? LastInitiate { get; private set; }
    /// <summary>
    /// 获取最近一次完成上传的请求。
    /// </summary>
    public CompleteMultipartUploadRequest? LastComplete { get; private set; }
    /// <summary>
    /// 获取或设置上传完成后执行的测试回调。
    /// </summary>
    public Func<Task>? AfterUpload { get; set; }
    /// <summary>
    /// 获取或设置终止上传前执行的测试回调。
    /// </summary>
    public Func<Task>? BeforeAbort { get; set; }
    /// <summary>
    /// 获取或设置是否模拟丢失合并上传响应。
    /// </summary>
    public bool LoseCompleteResponse { get; set; }
    /// <summary>
    /// 获取或设置是否省略远端校验和。
    /// </summary>
    public bool OmitRemoteChecksum { get; set; } = true;
    /// <summary>
    /// 获取查询分片列表的累计调用次数。
    /// </summary>
    public int ListCalls { get; private set; }
    /// <summary>
    /// 获取终止上传的累计调用次数。
    /// </summary>
    public int AbortCalls { get; private set; }
    /// <summary>
    /// 获取或设置是否模拟拒绝存储访问。
    /// </summary>
    public bool DenyAccess { get; set; }
    /// <summary>
    /// 组合存储桶及对象键作为对象标识。
    /// </summary>
    /// <param name="bucket">对象所属的存储桶。</param>
    /// <param name="key">用于关联测试操作的键。</param>
    /// <returns>由存储桶和对象键组成的对象标识。</returns>
    public static string ObjectId(string bucket, string key) => bucket + "/" + key;
    /// <summary>
    /// 检查取消及访问权限并记录 S3 操作。
    /// </summary>
    /// <param name="op">待记录的 S3 操作名称。</param>
    /// <param name="bucket">对象所属的存储桶。</param>
    /// <param name="key">用于关联测试操作的键。</param>
    /// <param name="ct">用于取消当前操作的令牌。</param>
    private void Record(string op, string bucket, string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (DenyAccess) throw new AmazonS3Exception("denied") { ErrorCode = "AccessDenied", StatusCode = HttpStatusCode.Forbidden };
        Calls.Add((op, bucket, key));
    }
    /// <summary>
    /// 查找模拟分片上传会话。
    /// </summary>
    /// <param name="id">目标对象或操作的标识。</param>
    /// <returns>找到的上传会话；不存在时抛出存储服务异常。</returns>
    private Upload Find(string id) => Uploads.TryGetValue(id, out var value) ? value : throw new AmazonS3Exception("gone")
    { ErrorCode = "NoSuchUpload", StatusCode = HttpStatusCode.NotFound };
    /// <inheritdoc />
    public override Task<InitiateMultipartUploadResponse> InitiateMultipartUploadAsync(InitiateMultipartUploadRequest request, CancellationToken ct = default)
    {
        Record("create", request.BucketName, request.Key, ct); LastInitiate = request;
        var id = Guid.NewGuid().ToString("N");
        Uploads[id] = new Upload { Bucket = request.BucketName, Key = request.Key,
            Metadata = request.Metadata.Keys.ToDictionary(x => x, x => request.Metadata[x]) };
        return Task.FromResult(new InitiateMultipartUploadResponse { UploadId = id });
    }
    /// <inheritdoc />
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
    /// <inheritdoc />
    public override Task<ListPartsResponse> ListPartsAsync(ListPartsRequest request, CancellationToken ct = default)
    {
        Record("list", request.BucketName, request.Key, ct); ListCalls++;
        ListRequests.Add((request.PartNumberMarker, request.MaxParts));
        var upload = Find(request.UploadId);
        var marker = int.TryParse(request.PartNumberMarker, out var m) ? m : 0;
        var all = upload.Parts.Where(x => x.Key > marker).OrderBy(x => x.Key).ToArray();
        var page = all.Take(request.MaxParts ?? 1000).ToArray();
        ListedParts += page.Length;
        return Task.FromResult(new ListPartsResponse { IsTruncated = page.Length < all.Length,
            NextPartNumberMarker = page.Length == 0 ? marker : page.Last().Key,
            Parts = page.Select(x => new PartDetail { PartNumber = x.Key, Size = x.Value.Length,
                ETag = x.Value.ETag, ChecksumSHA256 = OmitRemoteChecksum ? null : x.Value.Hash }).ToList() });
    }
    /// <inheritdoc />
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
    /// <inheritdoc />
    public override async Task<AbortMultipartUploadResponse> AbortMultipartUploadAsync(AbortMultipartUploadRequest request, CancellationToken ct = default)
    {
        Record("abort", request.BucketName, request.Key, ct); AbortCalls++;
        if (BeforeAbort != null) await BeforeAbort();
        Uploads.Remove(request.UploadId);
        return new AbortMultipartUploadResponse();
    }
    /// <inheritdoc />
    public override Task<GetObjectMetadataResponse> GetObjectMetadataAsync(GetObjectMetadataRequest request, CancellationToken ct = default)
    {
        Record("head", request.BucketName, request.Key, ct);
        var obj = GetObject(request.BucketName, request.Key);
        var result = new GetObjectMetadataResponse { ContentLength = obj.Bytes.Length, ETag = "object-etag", ContentType = "application/octet-stream" };
        foreach (var pair in obj.Metadata) result.Metadata.Add(pair.Key, pair.Value);
        return Task.FromResult(result);
    }
    /// <inheritdoc />
    public override Task<GetObjectResponse> GetObjectAsync(GetObjectRequest request, CancellationToken ct = default)
    {
        Record("get", request.BucketName, request.Key, ct);
        var obj = GetObject(request.BucketName, request.Key); var bytes = obj.Bytes;
        if (request.ByteRange != null) bytes = bytes.Skip((int)request.ByteRange.Start).Take((int)(request.ByteRange.End - request.ByteRange.Start + 1)).ToArray();
        return Task.FromResult(new GetObjectResponse { ResponseStream = new MemoryStream(bytes), ContentLength = bytes.Length });
    }
    /// <inheritdoc />
    public override async Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request, CancellationToken ct = default)
    {
        Record("put", request.BucketName, request.Key, ct);
        using var memory = new MemoryStream(); await request.InputStream.CopyToAsync(memory, ct);
        Objects[ObjectId(request.BucketName, request.Key)] = new StoredObject(memory.ToArray(), request.Metadata.Keys.ToDictionary(x => x, x => request.Metadata[x]));
        return new PutObjectResponse();
    }
    /// <inheritdoc />
    public override Task<DeleteObjectResponse> DeleteObjectAsync(DeleteObjectRequest request, CancellationToken ct = default)
    {
        Record("delete", request.BucketName, request.Key, ct); Objects.Remove(ObjectId(request.BucketName, request.Key));
        return Task.FromResult(new DeleteObjectResponse());
    }
    /// <summary>
    /// 查找模拟 S3 存储对象。
    /// </summary>
    /// <param name="bucket">对象所属的存储桶。</param>
    /// <param name="key">用于关联测试操作的键。</param>
    /// <returns>找到的存储对象；不存在时抛出存储服务异常。</returns>
    private StoredObject GetObject(string bucket, string key) => Objects.TryGetValue(ObjectId(bucket, key), out var obj) ? obj :
        throw new AmazonS3Exception("missing") { ErrorCode = "NoSuchKey", StatusCode = HttpStatusCode.NotFound };
    /// <summary>
    /// 保存录制的 S3 分片上传所需的桶、对象键和分片数据。
    /// </summary>
    public sealed class Upload
    {
        /// <summary>
        /// 保存模拟上传记录的对象存储桶和键。
        /// </summary>
        public string Bucket = "", Key = "";
        /// <summary>
        /// 保存模拟上传记录的元数据。
        /// </summary>
        public Dictionary<string, string> Metadata = new();
        /// <summary>
        /// 保存模拟上传记录的分片。
        /// </summary>
        public Dictionary<int, Part> Parts = new();
    }
    /// <summary>
    /// 保存已上传分片的字节、ETag 和哈希值。
    /// </summary>
    /// <param name="Bytes">用于初始化当前实例的Bytes值。</param>
    /// <param name="ETag">用于初始化当前实例的ETag值。</param>
    /// <param name="Hash">用于初始化当前实例的Hash值。</param>
    public sealed record Part(byte[] Bytes, string ETag, string Hash)
    {
        /// <summary>
        /// 获取或初始化文件分片的声明长度。
        /// </summary>
        /// <remarks>元数据成本测试可指定声明长度以避免分配整个文件；实际上传默认使用字节长度。</remarks>
        public long Length { get; init; } = Bytes.LongLength;
    }
    /// <summary>
    /// 保存测试对象的字节和元数据。
    /// </summary>
    /// <param name="Bytes">用于初始化当前实例的Bytes值。</param>
    /// <param name="Metadata">用于初始化当前实例的Metadata值。</param>
    public sealed record StoredObject(byte[] Bytes, Dictionary<string, string> Metadata);
}

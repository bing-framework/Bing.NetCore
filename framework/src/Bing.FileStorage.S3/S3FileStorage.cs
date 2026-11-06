using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace Bing.FileStorage;

/// <summary>
/// 绑定操作范围的 S3 文件存储门面。
/// </summary>
/// <remarks>
/// 绑定一个租户或主机范围的 S3 对象存储门面。
/// </remarks>
internal sealed class S3FileStorage : IFileStorage, IMultipartFileStorage, IMultipartFilePartReader, ISignedFileRequestProvider, IFileRangeReader
{
    /// <summary>
    /// 定义 S3 上传令牌在对象元数据中使用的键。
    /// </summary>
    private const string UploadTokenMetadata = "bing-upload-token";
    /// <summary>
    /// 定义 S3 上传模式在对象元数据中使用的键。
    /// </summary>
    private const string UploadModeMetadata = "bing-upload-mode";
    /// <summary>
    /// 定义 S3 预签名地址的最长有效期。
    /// </summary>
    private static readonly TimeSpan MaxSignatureLifetime = TimeSpan.FromDays(7);

    /// <summary>
    /// 保存 S3 存储使用的命名空间前缀。
    /// </summary>
    private readonly string _storageNamespace;
    /// <summary>
    /// 保存 S3 客户端实例。
    /// </summary>
    private readonly IAmazonS3 _client;
    /// <summary>
    /// 保存列举分片时使用的分页大小。
    /// </summary>
    private readonly int _listPartsPageSize;
    /// <summary>
    /// 保存容器名称到 S3 存储桶的映射。
    /// </summary>
    private readonly IReadOnlyDictionary<string, string> _containerBuckets;
    /// <summary>
    /// 保存 S3 上传使用的模式。
    /// </summary>
    private readonly S3UploadMode _uploadMode;
    /// <summary>
    /// 获取是否通过服务器代理上传。
    /// </summary>
    private bool ServerProxy => _uploadMode == S3UploadMode.ServerProxy;

    /// <summary>
    /// 初始化一个 <see cref="S3FileStorage" /> 类型的实例。
    /// </summary>
    /// <param name="name">实例使用的名称。</param>
    /// <param name="storageNamespace">文件对象使用的逻辑存储命名空间。</param>
    /// <param name="scope">实例所属的作用范围。</param>
    /// <param name="client">用于访问对应后端或服务的客户端。</param>
    /// <param name="listPartsPageSize">列出分片时每页读取的分片数量。</param>
    /// <param name="containerBuckets">逻辑容器名称到后端存储桶名称的映射。</param>
    /// <param name="uploadMode">文件上传所采用的模式。</param>
    public S3FileStorage(string name, string storageNamespace, StorageScope scope, IAmazonS3 client, int listPartsPageSize,
        IReadOnlyDictionary<string, string> containerBuckets, S3UploadMode uploadMode)
    {
        Name = name;
        _storageNamespace = storageNamespace;
        Scope = scope;
        _client = client;
        _listPartsPageSize = listPartsPageSize;
        _containerBuckets = containerBuckets;
        _uploadMode = uploadMode;
    }

    /// <inheritdoc />
    public string Name { get; }
    /// <inheritdoc />
    public StorageScope Scope { get; }
    /// <inheritdoc />
    public FileStorageCapabilities Capabilities =>
        FileStorageCapabilities.Multipart | FileStorageCapabilities.SignedDownload |
        (ServerProxy ? FileStorageCapabilities.ServerVerifiedParts : FileStorageCapabilities.SignedParts) | FileStorageCapabilities.Range;

    /// <inheritdoc />
    public async Task<FileWriteResult> SaveAsync(FileWriteRequest request, Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        FileStorageStreams.ValidateSource(source, request.Length);
        if (request.Mode == FileWriteMode.CreateOnly)
            throw new FileStorageException(FileStorageError.UnsupportedCapability, "S3 Provider 不提供原子 CreateOnly 保证。");

        _ = Bucket(request.Address);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Length == 0)
            return await SaveEmptyAsync(request, source, cancellationToken).ConfigureAwait(false);

        var partSize = Math.Max(5 * 1024 * 1024, (int)Math.Min(128 * 1024 * 1024, request.Length));
        var upload = await CreateMultipartAsync(request, partSize, cancellationToken).ConfigureAwait(false);
        try
        {
            var parts = new List<FilePart>(upload.PartCount);
            for (var partNumber = 1; partNumber <= upload.PartCount; partNumber++)
            {
                using var boundedSource = new BoundedReadStream(source, upload.GetPartLength(partNumber));
                var part = await UploadPartAsync(upload, partNumber, boundedSource, cancellationToken: cancellationToken).ConfigureAwait(false);
                parts.Add(part);
            }

            _ = await FileStorageStreams.CopyExactAsync(source, Stream.Null, 0, cancellationToken).ConfigureAwait(false);
            return await CompleteMultipartAsync(upload, parts, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            try { await AbortMultipartAsync(upload, CancellationToken.None).ConfigureAwait(false); }
            catch { }
            throw;
        }
    }

    /// <inheritdoc />
    public async Task ReadAsync(FileAddress address, Func<Stream, CancellationToken, Task> consume, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(consume);
        _ = Bucket(address);
        cancellationToken.ThrowIfCancellationRequested();

        GetObjectResponse response;
        try
        {
            response = await _client.GetObjectAsync(ObjectRequest(address), cancellationToken).ConfigureAwait(false);
        }
        catch (AmazonS3Exception ex)
        {
            throw MapException(ex);
        }
        catch (AmazonServiceException ex)
        {
            throw MapException(ex);
        }
        catch (HttpRequestException ex)
        {
            throw new FileStorageException(FileStorageError.Unavailable, "读取 S3 对象时连接失败。", ex);
        }

        using (response)
            await consume(response.ResponseStream, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<FileProperties?> GetPropertiesAsync(FileAddress address, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        _ = Bucket(address);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var response = await _client.GetObjectMetadataAsync(Bucket(address), ObjectKey(address), cancellationToken).ConfigureAwait(false);
            return ToProperties(response);
        }
        catch (AmazonS3Exception ex) when (IsMissingObject(ex))
        {
            return null;
        }
        catch (AmazonS3Exception ex)
        {
            throw MapException(ex);
        }
        catch (AmazonServiceException ex)
        {
            throw MapException(ex);
        }
        catch (HttpRequestException ex)
        {
            throw new FileStorageException(FileStorageError.Unavailable, "查询 S3 对象时连接失败。", ex);
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(FileAddress address, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        _ = Bucket(address);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await _client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = Bucket(address),
                Key = ObjectKey(address)
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (AmazonS3Exception ex)
        {
            throw MapException(ex, uncertain: true);
        }
        catch (AmazonServiceException ex)
        {
            throw MapException(ex, uncertain: true);
        }
        catch (HttpRequestException ex)
        {
            throw new FileStorageException(FileStorageError.OutcomeUnknown, "删除 S3 对象的结果未知。", ex);
        }
    }

    /// <inheritdoc />
    public async Task ReadRangeAsync(FileAddress address, long offset, long length,
        Func<Stream, CancellationToken, Task> consume, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(consume);
        if (offset < 0 || length < 0)
            throw new ArgumentOutOfRangeException(offset < 0 ? nameof(offset) : nameof(length));
        long endExclusive;
        try { endExclusive = checked(offset + length); }
        catch (OverflowException ex)
        { throw new FileStorageException(FileStorageError.InvalidLength, "Range 超出可表示的对象偏移。", ex); }
        _ = Bucket(address);
        cancellationToken.ThrowIfCancellationRequested();

        if (length == 0)
        {
            var properties = await GetPropertiesAsync(address, cancellationToken).ConfigureAwait(false);
            if (properties == null)
                throw new FileStorageException(FileStorageError.NotFound, "S3 对象不存在。");
            if (offset > properties.Length)
                throw new FileStorageException(FileStorageError.InvalidLength, "Range 起点超出对象长度。");
            using var empty = new MemoryStream(Array.Empty<byte>(), writable: false);
            await consume(empty, cancellationToken).ConfigureAwait(false);
            return;
        }

        var request = new GetObjectRequest { BucketName = Bucket(address), Key = ObjectKey(address) };
        request.ByteRange = new ByteRange(offset, endExclusive - 1);
        GetObjectResponse response;
        try
        {
            response = await _client.GetObjectAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (AmazonS3Exception ex)
        {
            throw MapException(ex);
        }
        catch (AmazonServiceException ex)
        {
            throw MapException(ex);
        }
        catch (HttpRequestException ex)
        {
            throw new FileStorageException(FileStorageError.Unavailable, "读取 S3 Range 时连接失败。", ex);
        }

        using (response)
        {
            if (response.ContentLength != length)
                throw new FileStorageException(FileStorageError.InvalidLength, "S3 返回的 Range 长度与请求不一致。");
            await consume(new BoundedReadStream(response.ResponseStream, length), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<MultipartUpload> CreateMultipartAsync(FileWriteRequest request, int partSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Mode == FileWriteMode.CreateOnly)
            throw new FileStorageException(FileStorageError.UnsupportedCapability, "S3 Provider 不提供原子 CreateOnly 保证。");
        if (request.Length == 0)
            throw new FileStorageException(FileStorageError.InvalidLength, "空文件必须通过服务端普通写入提交。");
        _ = new MultipartUpload("validation", request, partSize);
        _ = Bucket(request.Address);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Metadata.Keys.Any(FileWriteRequest.IsMultipartMetadata))
            throw new ArgumentException("文件元数据不能使用 Provider 保留键 bing-upload-token。", nameof(request));

        var token = Guid.NewGuid().ToString("N");
        var metadata = request.Metadata.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        metadata.Add(UploadTokenMetadata, token);
        metadata.Add(UploadModeMetadata, _uploadMode.ToString());
        ValidateMetadataSize(metadata);
        var persistedRequest = FileWriteRequest.RestoreMultipart(request.Address, request.Length, request.Mode, request.ContentType, metadata);
        var initiate = new InitiateMultipartUploadRequest
        {
            BucketName = Bucket(request.Address),
            Key = ObjectKey(request.Address),
            ContentType = request.ContentType,
            ChecksumAlgorithm = ServerProxy ? null : ChecksumAlgorithm.SHA256
        };
        foreach (var pair in metadata)
            initiate.Metadata.Add(pair.Key, pair.Value);

        try
        {
            var response = await _client.InitiateMultipartUploadAsync(initiate, cancellationToken).ConfigureAwait(false);
            return new MultipartUpload(response.UploadId, persistedRequest, partSize);
        }
        catch (AmazonS3Exception ex)
        {
            throw MapException(ex, uncertain: true);
        }
        catch (AmazonServiceException ex)
        {
            throw MapException(ex, uncertain: true);
        }
        catch (HttpRequestException ex)
        {
            throw new FileStorageException(FileStorageError.OutcomeUnknown, "创建 S3 分片会话的结果未知。", ex);
        }
    }

    /// <inheritdoc />
    public async Task<FilePart> UploadPartAsync(MultipartUpload upload, int partNumber, Stream source,
        string? sha256 = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(upload);
        ArgumentNullException.ThrowIfNull(source);
        FileStorageStreams.ValidateSha256(sha256);
        var length = upload.GetPartLength(partNumber);
        FileStorageStreams.ValidateSource(source, length);
        EnsureUploadRequest(upload);
        cancellationToken.ThrowIfCancellationRequested();

        var temporaryPath = Path.Combine(Path.GetTempPath(), "bing-s3-part-" + Guid.NewGuid().ToString("N") + ".tmp");
        FileStream staged;
        try
        {
            staged = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
                128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);
        }
        catch (IOException ex)
        {
            throw new FileStorageException(FileStorageError.CapacityExceeded, "无法在临时目录创建 S3 分片文件。", ex);
        }
        await using var stagedLifetime = staged;
        string actual;
        try
        {
            actual = await FileStorageStreams.CopyExactAsync(source, staged, length, cancellationToken).ConfigureAwait(false);
            if (sha256 != null && !FixedEquals(sha256, actual))
                throw new FileStorageException(FileStorageError.InvalidChecksum, "分片 SHA-256 与输入流不一致。");
            staged.Position = 0;
        }
        catch (IOException ex)
        {
            throw new FileStorageException(FileStorageError.CapacityExceeded, "无法在临时目录暂存 S3 分片。", ex);
        }

        var request = new UploadPartRequest
        {
            BucketName = Bucket(upload.Request.Address),
            Key = ObjectKey(upload.Request.Address),
            UploadId = upload.UploadId,
            PartNumber = partNumber,
            PartSize = length,
            InputStream = staged,
            ChecksumAlgorithm = ServerProxy ? null : ChecksumAlgorithm.SHA256,
            ChecksumSHA256 = ServerProxy ? null : actual,
            UseChunkEncoding = !ServerProxy,
            DisablePayloadSigning = false
        };

        try
        {
            var response = await _client.UploadPartAsync(request, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(response.ETag) || (response.PartNumber.HasValue && response.PartNumber.Value != partNumber))
                throw new FileStorageException(FileStorageError.InvalidPart, "S3 返回的分片回执无效。");
            if (!string.IsNullOrEmpty(response.ChecksumSHA256) && !FixedEquals(response.ChecksumSHA256, actual))
                throw new FileStorageException(FileStorageError.InvalidChecksum, "S3 返回的分片 SHA-256 与输入流不一致。");

            return new FilePart
            {
                Number = response.PartNumber ?? partNumber,
                Length = length,
                ETag = response.ETag,
                Sha256 = actual
            };
        }
        catch (FileStorageException)
        {
            throw;
        }
        catch (AmazonS3Exception ex)
        {
            throw MapException(ex, uncertain: true);
        }
        catch (AmazonServiceException ex)
        {
            throw MapException(ex, uncertain: true);
        }
        catch (HttpRequestException ex)
        {
            throw new FileStorageException(FileStorageError.OutcomeUnknown, "上传 S3 分片的结果未知。", ex);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FilePart>> ListPartsAsync(MultipartUpload upload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(upload);
        EnsureUploadRequest(upload);
        var parts = new List<FilePart>();
        int? marker = null;
        do
        {
            var page = await ReadPartPageAsync(upload, marker, _listPartsPageSize, cancellationToken).ConfigureAwait(false);
            parts.AddRange(page.Parts);
            marker = page.NextMarker;
        } while (marker.HasValue);
        return parts;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>利用排他游标只读取目标位置的一条记录，不重复枚举此前已上传的分片。</para>
    /// </remarks>
    public async Task<FilePart?> GetPartAsync(MultipartUpload upload, int partNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(upload);
        EnsureUploadRequest(upload);
        upload.GetPartLength(partNumber);
        var page = await ReadPartPageAsync(upload, partNumber - 1, 1, cancellationToken).ConfigureAwait(false);
        var part = page.Parts.FirstOrDefault();
        // 分片编号可以有空洞，后继分片不代表目标分片存在。
        return part?.Number == partNumber ? part : null;
    }

    /// <summary>
    /// 读取并校验一个 S3 分片分页结果。
    /// </summary>
    /// <param name="upload">要读取分片列表的上传会话。</param>
    /// <param name="marker">上一页最后一个分片编号；为 null 时从第一页开始读取。</param>
    /// <param name="maxParts">本页允许返回的最大分片数。</param>
    /// <param name="cancellationToken">用于取消请求的令牌。</param>
    /// <returns>已校验的分片列表，以及存在后续页时的下一页标记；没有后续页时标记为 null。</returns>
    private async Task<(IReadOnlyList<FilePart> Parts, int? NextMarker)> ReadPartPageAsync(MultipartUpload upload,
        int? marker, int maxParts, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await _client.ListPartsAsync(new ListPartsRequest
            {
                BucketName = Bucket(upload.Request.Address),
                Key = ObjectKey(upload.Request.Address),
                UploadId = upload.UploadId,
                PartNumberMarker = marker?.ToString(CultureInfo.InvariantCulture),
                MaxParts = maxParts
            }, cancellationToken).ConfigureAwait(false);

            var details = response.Parts ?? new List<PartDetail>();
            if (details.Count > maxParts)
                throw new FileStorageException(FileStorageError.InvalidPart, "S3 分片页面超过请求的条数上限。");
            var parts = new List<FilePart>(details.Count);
            var previous = marker ?? 0;
            foreach (var part in details)
            {
                if (part.PartNumber == null || part.PartNumber <= previous || part.PartNumber > 10000 ||
                    part.Size == null || part.Size < 0 || string.IsNullOrWhiteSpace(part.ETag))
                    throw new FileStorageException(FileStorageError.InvalidPart, "S3 返回了无效或无序的分片属性。");
                parts.Add(new FilePart
                {
                    Number = part.PartNumber.Value,
                    Length = part.Size.Value,
                    ETag = part.ETag,
                    Sha256 = part.ChecksumSHA256
                });
                previous = part.PartNumber.Value;
            }

            if (response.IsTruncated != true)
                return (parts, null);
            // S3 的续页标记为本页最后序号；拒绝倒退、跳号游标或空的截断页。
            var next = response.NextPartNumberMarker;
            if (parts.Count == 0 || next != previous || next <= (marker ?? 0))
                throw new FileStorageException(FileStorageError.OutcomeUnknown, "S3 分片分页游标无效或未前进。");
            return (parts, next);
        }
        catch (AmazonS3Exception ex)
        {
            throw MapException(ex);
        }
        catch (AmazonServiceException ex)
        {
            throw MapException(ex);
        }
        catch (HttpRequestException ex)
        {
            throw new FileStorageException(FileStorageError.Unavailable, "查询 S3 分片时连接失败。", ex);
        }
    }

    /// <inheritdoc />
    public async Task<FileWriteResult> CompleteMultipartAsync(MultipartUpload upload, IReadOnlyList<FilePart> parts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(upload);
        ArgumentNullException.ThrowIfNull(parts);
        EnsureUploadRequest(upload);
        FileStorageStreams.ValidateParts(upload, parts);
        if (ServerProxy)
            foreach (var part in parts)
            {
                if (part.Sha256 == null) throw new FileStorageException(FileStorageError.InvalidChecksum, "代理分片缺少服务端已验证摘要。");
                FileStorageStreams.ValidateSha256(part.Sha256);
            }
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<FilePart> actualParts;
        try { actualParts = await ListPartsAsync(upload, cancellationToken).ConfigureAwait(false); }
        catch (FileStorageException ex) when (ex.Error == FileStorageError.UploadNotFound)
        {
            // 重复完成或响应丢失后，上传句柄已消失；只有同一个 Provider 会话令牌才能确认成功。
            var completed = await GetPropertiesAsync(upload.Request.Address, cancellationToken).ConfigureAwait(false);
            if (completed != null && completed.Length == upload.Request.Length && MatchesUploadToken(upload, completed))
                return new FileWriteResult(new FileLocation(Name, Scope, upload.Request.Address), completed);
            throw;
        }
        FileStorageStreams.ValidateParts(upload, actualParts);
        if (!PartsMatch(parts, actualParts))
            throw new FileStorageException(FileStorageError.InvalidPart, "提交的分片清单与 S3 服务端清单不一致。");

        try
        {
            await _client.CompleteMultipartUploadAsync(new CompleteMultipartUploadRequest
            {
                BucketName = Bucket(upload.Request.Address),
                Key = ObjectKey(upload.Request.Address),
                UploadId = upload.UploadId,
                MpuObjectSize = ServerProxy ? null : upload.Request.Length,
                PartETags = actualParts.Select(part => new PartETag(part.Number, part.ETag)
                {
                    ChecksumSHA256 = ServerProxy ? null : part.Sha256
                }).ToList()
            }, cancellationToken).ConfigureAwait(false);

            return await ConfirmCompletedObjectAsync(upload, cancellationToken).ConfigureAwait(false);
        }
        catch (FileStorageException)
        {
            throw;
        }
        catch (AmazonS3Exception ex)
        {
            return await ReconcileCompleteFailureAsync(upload, ex, cancellationToken).ConfigureAwait(false);
        }
        catch (AmazonServiceException ex)
        {
            return await ReconcileCompleteFailureAsync(upload, ex, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            return await ReconcileCompleteFailureAsync(upload, ex, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task AbortMultipartAsync(MultipartUpload upload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(upload);
        EnsureUploadRequest(upload);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await _client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest
            {
                BucketName = Bucket(upload.Request.Address),
                Key = ObjectKey(upload.Request.Address),
                UploadId = upload.UploadId
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (AmazonS3Exception ex) when (IsNoSuchUpload(ex))
        {
        }
        catch (AmazonS3Exception ex)
        {
            throw MapException(ex, uncertain: true);
        }
        catch (AmazonServiceException ex)
        {
            throw MapException(ex, uncertain: true);
        }
        catch (HttpRequestException ex)
        {
            throw new FileStorageException(FileStorageError.OutcomeUnknown, "中止 S3 分片会话的结果未知。", ex);
        }
    }

    /// <inheritdoc />
    public Task<SignedFileRequest> SignDownloadAsync(FileAddress address, TimeSpan lifetime, string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        ValidateLifetime(lifetime);
        _ = Bucket(address);
        if (fileName != null && (fileName.Length > 255 || fileName.Any(char.IsControl)))
            throw new ArgumentException("下载文件名无效。", nameof(fileName));
        cancellationToken.ThrowIfCancellationRequested();

        var expiresAt = DateTimeOffset.UtcNow.Add(lifetime);
        var request = new GetPreSignedUrlRequest
        {
            BucketName = Bucket(address),
            Key = ObjectKey(address),
            Verb = HttpVerb.GET,
            Protocol = _client.Config.UseHttp ? Protocol.HTTP : Protocol.HTTPS,
            Expires = expiresAt.UtcDateTime
        };
        if (fileName != null)
            request.ResponseHeaderOverrides.ContentDisposition = "attachment; filename*=UTF-8''" + Uri.EscapeDataString(fileName);

        try
        {
            var url = _client.GetPreSignedURL(request);
            return Task.FromResult(new SignedFileRequest
            {
                Url = new Uri(url, UriKind.Absolute),
                Method = "GET",
                ExpiresAt = expiresAt
            });
        }
        catch (AmazonServiceException ex)
        {
            throw MapException(ex);
        }
    }

    /// <inheritdoc />
    public Task<SignedFileRequest> SignPartAsync(MultipartUpload upload, int partNumber, string sha256,
        TimeSpan lifetime, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(upload);
        if (ServerProxy) throw new FileStorageException(FileStorageError.UnsupportedCapability, "代理上传实例不签发分片直传请求。");
        FileStorageStreams.ValidateSha256(sha256);
        if (sha256 == null)
            throw new ArgumentNullException(nameof(sha256), "直传分片必须提供 SHA-256。");
        var partLength = upload.GetPartLength(partNumber);
        EnsureUploadRequest(upload);
        ValidateLifetime(lifetime);
        cancellationToken.ThrowIfCancellationRequested();

        var expiresAt = DateTimeOffset.UtcNow.Add(lifetime);
        var request = new GetPreSignedUrlRequest
        {
            BucketName = Bucket(upload.Request.Address),
            Key = ObjectKey(upload.Request.Address),
            UploadId = upload.UploadId,
            PartNumber = partNumber,
            Verb = HttpVerb.PUT,
            Protocol = _client.Config.UseHttp ? Protocol.HTTP : Protocol.HTTPS,
            Expires = expiresAt.UtcDateTime
        };
        request.Headers.ContentLength = partLength;
        request.Headers["x-amz-checksum-sha256"] = sha256;

        try
        {
            var url = _client.GetPreSignedURL(request);
            return Task.FromResult(new SignedFileRequest
            {
                Url = new Uri(url, UriKind.Absolute),
                Method = "PUT",
                ExpiresAt = expiresAt,
                Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Content-Length"] = partLength.ToString(CultureInfo.InvariantCulture),
                    ["x-amz-checksum-sha256"] = sha256
                }
            });
        }
        catch (AmazonServiceException ex)
        {
            throw MapException(ex);
        }
    }

    /// <summary>
    /// 写入空文件并确认对象写入结果。
    /// </summary>
    /// <param name="request">包含目标地址、内容类型和元数据的写入请求。</param>
    /// <param name="source">应为空的源数据流。</param>
    /// <param name="cancellationToken">用于取消写入及结果查询的令牌。</param>
    /// <returns>确认写入成功的文件位置和属性。</returns>
    /// <remarks>
    /// 方法为写入生成 Provider 上传标记；发生服务调用异常时会查询对象并核对标记，以确认写入结果。
    /// </remarks>
    private async Task<FileWriteResult> SaveEmptyAsync(FileWriteRequest request, Stream source, CancellationToken cancellationToken)
    {
        var operationToken = Guid.NewGuid().ToString("N");
        var metadata = request.Metadata.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        if (metadata.Keys.Any(FileWriteRequest.IsMultipartMetadata))
            throw new ArgumentException("文件元数据不能使用 Provider 保留键 bing-upload-token。", nameof(request));
        metadata[UploadTokenMetadata] = operationToken;
        metadata[UploadModeMetadata] = _uploadMode.ToString();
        ValidateMetadataSize(metadata);
        await FileStorageStreams.CopyExactAsync(source, Stream.Null, 0, cancellationToken).ConfigureAwait(false);
        var put = new PutObjectRequest
        {
            BucketName = Bucket(request.Address),
            Key = ObjectKey(request.Address),
            ContentType = request.ContentType,
            // 已验证调用方流为空；向 SDK 提供可 Seek 的空流，防止非 Seek 输入触发 aws-chunked 签名。
            InputStream = Stream.Null,
            AutoCloseStream = false,
            IfNoneMatch = null,
            UseChunkEncoding = !ServerProxy,
            DisablePayloadSigning = false
        };
        put.Headers.ContentLength = 0;
        foreach (var pair in metadata)
            put.Metadata.Add(pair.Key, pair.Value);

        try
        {
            await _client.PutObjectAsync(put, cancellationToken).ConfigureAwait(false);
            var properties = await GetPropertiesAsync(request.Address, cancellationToken).ConfigureAwait(false)
                ?? throw new FileStorageException(FileStorageError.OutcomeUnknown, "S3 已确认写入但对象属性不可见。");
            return new FileWriteResult(new FileLocation(Name, Scope, request.Address), properties);
        }
        catch (FileStorageException)
        {
            throw;
        }
        catch (AmazonS3Exception ex)
        {
            return await ReconcileWriteFailureAsync(request, operationToken, ex, cancellationToken).ConfigureAwait(false);
        }
        catch (AmazonServiceException ex)
        {
            return await ReconcileWriteFailureAsync(request, operationToken, ex, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            return await ReconcileWriteFailureAsync(request, operationToken, ex, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 核对已完成的 S3 分片对象是否与上传会话一致。
    /// </summary>
    /// <param name="upload">已完成的分片上传会话。</param>
    /// <param name="cancellationToken">用于取消对象属性查询的令牌。</param>
    /// <returns>长度及上传标记均与会话匹配的文件位置和属性。</returns>
    private async Task<FileWriteResult> ConfirmCompletedObjectAsync(MultipartUpload upload, CancellationToken cancellationToken)
    {
        var properties = await GetPropertiesAsync(upload.Request.Address, cancellationToken).ConfigureAwait(false);
        if (properties == null || properties.Length != upload.Request.Length || !MatchesUploadToken(upload, properties))
            throw new FileStorageException(FileStorageError.OutcomeUnknown,
                "S3 分片完成后对象属性未能与会话核对（对象=" + (properties == null ? "missing" : properties.Length.ToString(CultureInfo.InvariantCulture)) +
                ", 元数据键=" + (properties == null ? "" : string.Join(",", properties.Metadata.Keys)) + "）。");
        return new FileWriteResult(new FileLocation(Name, Scope, upload.Request.Address), properties);
    }

    /// <summary>
    /// 根据对象状态核对 S3 分片完成操作的失败结果。
    /// </summary>
    /// <param name="upload">待核对的分片上传会话。</param>
    /// <param name="failure">分片完成操作抛出的异常。</param>
    /// <param name="cancellationToken">用于取消对象属性查询的令牌。</param>
    /// <returns>对象长度和上传标记与会话匹配时返回已确认的文件位置和属性。</returns>
    /// <remarks>
    /// 对象被其他上传标记占用时报告冲突；无法确认完成结果时报告结果未知。
    /// </remarks>
    private async Task<FileWriteResult> ReconcileCompleteFailureAsync(MultipartUpload upload, Exception failure,
        CancellationToken cancellationToken)
    {
        try
        {
            var properties = await GetPropertiesAsync(upload.Request.Address, cancellationToken).ConfigureAwait(false);
            if (properties != null && properties.Length == upload.Request.Length && MatchesUploadToken(upload, properties))
                return new FileWriteResult(new FileLocation(Name, Scope, upload.Request.Address), properties);
            if (properties != null && properties.Metadata.ContainsKey(UploadTokenMetadata))
                throw new FileStorageException(FileStorageError.Conflict, "对象位置已被其他上传会话占用。", failure);
            if (failure is AmazonS3Exception s3 && IsNoSuchUpload(s3) && properties == null)
                throw MapException(s3);
            if (failure is AmazonServiceException knownFailure)
            {
                var mapped = MapException(knownFailure, uncertain: true);
                if (mapped.Error is not (FileStorageError.OutcomeUnknown or FileStorageError.Unavailable))
                    throw mapped;
            }
        }
        catch (FileStorageException ex) when (ex.Error == FileStorageError.Conflict)
        {
            throw;
        }
        catch (FileStorageException ex) when (ex.Error is FileStorageError.UploadNotFound or FileStorageError.InvalidPart or
            FileStorageError.InvalidChecksum or FileStorageError.AccessDenied or FileStorageError.Configuration)
        {
            throw;
        }
        catch (FileStorageException ex) when (ex.Error == FileStorageError.NotFound)
        {
        }
        catch (FileStorageException)
        {
            throw new FileStorageException(FileStorageError.OutcomeUnknown, "S3 完成结果未知，且无法完成对象对账。", failure);
        }

        throw new FileStorageException(FileStorageError.OutcomeUnknown, "S3 完成结果未知；对象尚未能与会话匹配。", failure,
            failure is AmazonServiceException serviceException ? serviceException.RequestId : null);
    }

    /// <summary>
    /// 根据对象状态核对 S3 写入操作的失败结果。
    /// </summary>
    /// <param name="request">原始文件写入请求。</param>
    /// <param name="token">本次写入使用的 Provider 上传标记。</param>
    /// <param name="failure">写入操作抛出的异常。</param>
    /// <param name="cancellationToken">用于取消对象属性查询的令牌。</param>
    /// <returns>对象长度和上传标记与本次写入匹配时返回已确认的文件位置和属性。</returns>
    /// <remarks>
    /// 无法确认写入结果时会映射原始异常或报告结果未知；对象被其他上传标记占用时报告冲突。
    /// </remarks>
    private async Task<FileWriteResult> ReconcileWriteFailureAsync(FileWriteRequest request, string token,
        Exception failure, CancellationToken cancellationToken)
    {
        try
        {
            var properties = await GetPropertiesAsync(request.Address, cancellationToken).ConfigureAwait(false);
            if (properties != null && properties.Length == request.Length &&
                properties.Metadata.TryGetValue(UploadTokenMetadata, out var actual) && actual == token)
                return new FileWriteResult(new FileLocation(Name, Scope, request.Address), properties);
            if (properties != null && properties.Metadata.ContainsKey(UploadTokenMetadata))
                throw new FileStorageException(FileStorageError.Conflict, "对象位置已被其他上传会话占用。", failure);
        }
        catch (FileStorageException ex) when (ex.Error == FileStorageError.Conflict)
        {
            throw;
        }
        catch (FileStorageException ex) when (ex.Error is FileStorageError.Unavailable or FileStorageError.OutcomeUnknown or FileStorageError.NotFound)
        {
        }

        throw failure switch
        {
            AmazonServiceException aws => MapException(aws, uncertain: true),
            HttpRequestException => new FileStorageException(FileStorageError.OutcomeUnknown, "S3 写入结果未知。", failure),
            _ => new FileStorageException(FileStorageError.OutcomeUnknown, "S3 写入结果未知。", failure)
        };
    }

    /// <summary>
    /// 检查对象属性中的上传标记是否与分片会话一致。
    /// </summary>
    /// <param name="upload">包含预期上传标记的分片上传会话。</param>
    /// <param name="properties">待检查对象的属性。</param>
    /// <returns>元数据中存在且两个上传标记相同时返回 true，否则返回 false。</returns>
    private bool MatchesUploadToken(MultipartUpload upload, FileProperties properties) =>
        upload.Request.Metadata.TryGetValue(UploadTokenMetadata, out var token) &&
        properties.Metadata.TryGetValue(UploadTokenMetadata, out var actual) && actual == token;

    /// <summary>
    /// 比较两组分片的编号、长度、ETag 和适用的校验和。
    /// </summary>
    /// <param name="expected">上传会话记录的预期分片。</param>
    /// <param name="actual">从存储服务读取的实际分片。</param>
    /// <returns>两组分片数量及逐项信息匹配时返回 true；数量或分片信息不匹配时返回 false。</returns>
    /// <remarks>
    /// 非代理模式下，预期校验和存在但与实际值不匹配时会抛出校验和错误。
    /// </remarks>
    private bool PartsMatch(IReadOnlyList<FilePart> expected, IReadOnlyList<FilePart> actual)
    {
        if (expected.Count != actual.Count)
            return false;
        for (var index = 0; index < expected.Count; index++)
        {
            if (expected[index].Number != actual[index].Number || expected[index].Length != actual[index].Length ||
                !string.Equals(expected[index].ETag, actual[index].ETag, StringComparison.Ordinal))
                return false;
            if (!ServerProxy && expected[index].Sha256 != null && !FixedEquals(expected[index].Sha256!, actual[index].Sha256 ?? string.Empty))
                throw new FileStorageException(FileStorageError.InvalidChecksum, "服务端分片 SHA-256 与登记值不一致。");
        }
        return true;
    }

    /// <summary>
    /// 将 S3 对象元数据响应转换为文件属性。
    /// </summary>
    /// <param name="response">存储服务返回的对象元数据响应。</param>
    /// <returns>由响应生成的文件属性。</returns>
    private FileProperties ToProperties(GetObjectMetadataResponse response)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in response.Metadata.Keys)
        {
            var normalizedKey = key.StartsWith("x-amz-meta-", StringComparison.OrdinalIgnoreCase)
                ? key["x-amz-meta-".Length..]
                : key;
            metadata[normalizedKey.ToLowerInvariant()] = response.Metadata[key];
        }
        return new FileProperties
        {
            Length = response.ContentLength,
            LastModifiedUtc = response.LastModified.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(response.LastModified.Value, DateTimeKind.Utc))
                : DateTimeOffset.MinValue,
            ContentType = response.ContentType,
            ETag = response.ETag,
            VersionId = response.VersionId,
            Metadata = metadata
        };
    }

    /// <summary>
    /// 创建用于访问指定文件地址的 S3 对象请求。
    /// </summary>
    /// <param name="address">目标文件地址。</param>
    /// <returns>包含目标存储桶和对象键的请求。</returns>
    private GetObjectRequest ObjectRequest(FileAddress address) => new()
    {
        BucketName = Bucket(address),
        Key = ObjectKey(address)
    };

    /// <summary>
    /// 解析并验证文件地址对应的 S3 存储桶。
    /// </summary>
    /// <param name="address">包含逻辑容器的文件地址。</param>
    /// <returns>配置映射后的有效 S3 存储桶名称。</returns>
    /// <remarks>
    /// 配置了逻辑容器映射但找不到对应项，或存储桶名称无效时会抛出配置错误。
    /// </remarks>
    private string Bucket(FileAddress address)
    {
        var bucket = address.Container;
        if (_containerBuckets.Count != 0 && !_containerBuckets.TryGetValue(address.Container, out bucket))
            throw new FileStorageException(FileStorageError.Configuration, "逻辑容器没有配置 S3 空间映射。");
        ValidateBucket(bucket);
        return bucket;
    }

    /// <summary>
    /// 生成指定文件地址对应的命名空间对象键。
    /// </summary>
    /// <param name="address">目标文件地址。</param>
    /// <returns>包含存储命名空间和作用域的对象键。</returns>
    private string ObjectKey(FileAddress address) => FileStorageStreams.ObjectKey(_storageNamespace, Scope, address);

    /// <summary>
    /// 验证分片上传请求与当前存储配置兼容。
    /// </summary>
    /// <param name="upload">待验证的分片上传会话。</param>
    /// <remarks>
    /// 验证目标存储桶和元数据长度，并检查会话记录的上传模式及 Provider 上传标记。
    /// </remarks>
    private void EnsureUploadRequest(MultipartUpload upload)
    {
        _ = Bucket(upload.Request.Address);
        ValidateMetadataSize(upload.Request.Metadata);
        // 无模式元数据的历史 S3 会话仅属于原先的强校验直传模式。
        var storedMode = upload.Request.Metadata.TryGetValue(UploadModeMetadata, out var mode) ? mode : nameof(S3UploadMode.SignedParts);
        if (!string.Equals(storedMode, _uploadMode.ToString(), StringComparison.Ordinal))
            throw new FileStorageException(FileStorageError.Conflict, "存储上传模式与持久化会话不一致，请恢复原实例配置。");
        if (upload.Request.Metadata.ContainsKey(UploadTokenMetadata) &&
            !IsToken(upload.Request.Metadata[UploadTokenMetadata]))
            throw new FileStorageException(FileStorageError.Conflict, "S3 上传会话的 Provider 元数据无效。");
    }

    /// <summary>
    /// 检查字符串是否为 32 位十六进制上传标记。
    /// </summary>
    /// <param name="value">待检查的字符串。</param>
    /// <returns>长度为 32 且全部由十六进制字符组成时返回 true，否则返回 false。</returns>
    private static bool IsToken(string value) => value.Length == 32 && value.All(Uri.IsHexDigit);

    /// <summary>
    /// 验证文件元数据的键和值长度总和未超过 S3 限制。
    /// </summary>
    /// <param name="metadata">待验证的元数据字典。</param>
    /// <remarks>
    /// 键和值长度总和超过 2 KiB 时抛出参数异常。
    /// </remarks>
    private static void ValidateMetadataSize(IReadOnlyDictionary<string, string> metadata)
    {
        // FileWriteRequest 已限定为 ASCII；S3 用户元数据键和值的合计上限为 2 KiB。
        if (metadata.Sum(p => p.Key.Length + p.Value.Length) > 2048)
            throw new ArgumentException("S3 元数据（包含 Provider 字段）不能超过 2 KiB。", nameof(metadata));
    }

    /// <summary>
    /// 验证字符串是否符合 S3 存储桶名称约束。
    /// </summary>
    /// <param name="bucket">待验证的存储桶名称。</param>
    /// <remarks>
    /// 名称为空、长度超出 3 至 63 个字符、采用 IP 地址格式或包含不符合约束的字符及组合时抛出配置错误。
    /// </remarks>
    internal static void ValidateBucket(string bucket)
    {
        if (string.IsNullOrEmpty(bucket) || bucket.Length is < 3 or > 63 || IPAddress.TryParse(bucket, out _) || bucket.Contains("..", StringComparison.Ordinal) ||
            bucket.Contains(".-", StringComparison.Ordinal) || bucket.Contains("-.", StringComparison.Ordinal) ||
            bucket[0] == '.' || bucket[^1] == '.' || bucket[0] == '-' || bucket[^1] == '-' ||
            bucket.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '-')))
            throw new FileStorageException(FileStorageError.Configuration,
                "逻辑容器不能作为合法 S3 Bucket 名称使用；Bucket 名称须为 3 至 63 个小写 DNS 字符。");
    }

    /// <summary>
    /// 检查 S3 异常是否表示对象不存在。
    /// </summary>
    /// <param name="ex">待检查的 S3 异常。</param>
    /// <returns>错误代码为 NoSuchKey 或 NotFound，或状态为 404 且错误代码不是 NoSuchBucket 时返回 true，否则返回 false。</returns>
    private static bool IsMissingObject(AmazonS3Exception ex) =>
        ex.ErrorCode is "NoSuchKey" or "NotFound" || ex.StatusCode == HttpStatusCode.NotFound && ex.ErrorCode != "NoSuchBucket";

    /// <summary>
    /// 检查 S3 异常是否表示分片上传不存在。
    /// </summary>
    /// <param name="ex">待检查的 S3 异常。</param>
    /// <returns>错误代码为 NoSuchUpload 时返回 true，否则返回 false。</returns>
    private static bool IsNoSuchUpload(AmazonS3Exception ex) => ex.ErrorCode == "NoSuchUpload";

    /// <summary>
    /// 验证签名有效期在允许范围内。
    /// </summary>
    /// <param name="lifetime">待验证的签名有效期。</param>
    /// <remarks>
    /// 有效期必须大于零且不超过最大签名有效期；超出范围时抛出参数异常。
    /// </remarks>
    private static void ValidateLifetime(TimeSpan lifetime)
    {
        if (lifetime <= TimeSpan.Zero || lifetime > MaxSignatureLifetime)
            throw new ArgumentOutOfRangeException(nameof(lifetime), "签名有效期必须大于 0 且不超过 7 天。");
    }

    /// <summary>
    /// 以固定时间比较两个 Base64 编码值。
    /// </summary>
    /// <param name="left">第一个 Base64 编码值。</param>
    /// <param name="right">第二个 Base64 编码值。</param>
    /// <returns>两个值可解码、长度相同且字节相同时返回 true；否则返回 false。</returns>
    private static bool FixedEquals(string left, string right)
    {
        try
        {
            var a = Convert.FromBase64String(left);
            var b = Convert.FromBase64String(right);
            return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// 将 Amazon 服务异常映射为文件存储异常。
    /// </summary>
    /// <param name="ex">待映射的 Amazon 服务异常。</param>
    /// <param name="uncertain">是否将未识别的失败映射为结果未知。</param>
    /// <returns>包含对应文件存储错误类别、原始异常和请求标识的异常对象。</returns>
    /// <remarks>
    /// 未识别的失败在 uncertain 为 true 时映射为结果未知，否则映射为服务不可用。
    /// </remarks>
    private static FileStorageException MapException(AmazonServiceException ex, bool uncertain = false)
    {
        var s3 = ex as AmazonS3Exception;
        FileStorageError? mappedError = s3?.ErrorCode switch
        {
            "NoSuchBucket" => FileStorageError.Configuration,
            "NoSuchKey" or "NotFound" => FileStorageError.NotFound,
            "NoSuchUpload" => FileStorageError.UploadNotFound,
            "AccessDenied" or "InvalidAccessKeyId" or "SignatureDoesNotMatch" => FileStorageError.AccessDenied,
            "InvalidBucketName" or "AuthorizationHeaderMalformed" or "InvalidRequest" => FileStorageError.Configuration,
            "InvalidPart" or "InvalidPartOrder" or "EntityTooSmall" => FileStorageError.InvalidPart,
            "BadDigest" or "InvalidDigest" or "XAmzContentSHA256Mismatch" => FileStorageError.InvalidChecksum,
            "PreconditionFailed" or "ConditionalRequestConflict" => FileStorageError.Conflict,
            _ => null
        };
        var error = mappedError ?? (ex.StatusCode switch
        {
            HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized => FileStorageError.AccessDenied,
            HttpStatusCode.NotFound when s3?.ErrorCode != "NoSuchBucket" => FileStorageError.NotFound,
            HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed => FileStorageError.Conflict,
            HttpStatusCode.RequestedRangeNotSatisfiable => FileStorageError.InvalidLength,
            HttpStatusCode.BadRequest => FileStorageError.Configuration,
            _ => uncertain ? FileStorageError.OutcomeUnknown : FileStorageError.Unavailable
        });

        return new FileStorageException(error, "S3 请求失败：" + (s3?.ErrorCode ?? ex.ErrorCode ?? ex.GetType().Name), ex, ex.RequestId);
    }

}

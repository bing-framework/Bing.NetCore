using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace Bing.FileStorage;

/// <summary>绑定一个租户或主机范围的 S3 对象存储门面。</summary>
internal sealed class S3FileStorage : IFileStorage, IMultipartFileStorage, ISignedFileRequestProvider, IFileRangeReader
{
    private const string UploadTokenMetadata = "bing-upload-token";
    private const string UploadModeMetadata = "bing-upload-mode";
    private static readonly TimeSpan MaxSignatureLifetime = TimeSpan.FromDays(7);

    private readonly string _storageNamespace;
    private readonly IAmazonS3 _client;
    private readonly int _listPartsPageSize;
    private readonly IReadOnlyDictionary<string, string> _containerBuckets;
    private readonly S3UploadMode _uploadMode;
    private bool ServerProxy => _uploadMode == S3UploadMode.ServerProxy;

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

    public string Name { get; }
    public StorageScope Scope { get; }
    public FileStorageCapabilities Capabilities =>
        FileStorageCapabilities.Multipart | FileStorageCapabilities.SignedDownload |
        (ServerProxy ? FileStorageCapabilities.ServerVerifiedParts : FileStorageCapabilities.SignedParts) | FileStorageCapabilities.Range;

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

    public async Task<IReadOnlyList<FilePart>> ListPartsAsync(MultipartUpload upload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(upload);
        EnsureUploadRequest(upload);
        var parts = new List<FilePart>();
        string? marker = null;
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var response = await _client.ListPartsAsync(new ListPartsRequest
                {
                    BucketName = Bucket(upload.Request.Address),
                    Key = ObjectKey(upload.Request.Address),
                    UploadId = upload.UploadId,
                    PartNumberMarker = marker,
                    MaxParts = _listPartsPageSize
                }, cancellationToken).ConfigureAwait(false);

                foreach (var part in response.Parts ?? new List<PartDetail>())
                {
                    if (part.PartNumber == null || part.Size == null)
                        throw new FileStorageException(FileStorageError.InvalidPart, "S3 返回了不完整的分片属性。");
                    parts.Add(new FilePart
                    {
                        Number = part.PartNumber.Value,
                        Length = part.Size.Value,
                        ETag = part.ETag,
                        Sha256 = part.ChecksumSHA256
                    });
                }

                if (response.IsTruncated != true)
                    break;
                var next = response.NextPartNumberMarker?.ToString(CultureInfo.InvariantCulture);
                if (string.IsNullOrEmpty(next) || string.Equals(marker, next, StringComparison.Ordinal))
                    throw new FileStorageException(FileStorageError.OutcomeUnknown, "S3 分片分页游标未前进。");
                marker = next;
            }

            parts.Sort((left, right) => left.Number.CompareTo(right.Number));
            return parts;
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

    private async Task<FileWriteResult> ConfirmCompletedObjectAsync(MultipartUpload upload, CancellationToken cancellationToken)
    {
        var properties = await GetPropertiesAsync(upload.Request.Address, cancellationToken).ConfigureAwait(false);
        if (properties == null || properties.Length != upload.Request.Length || !MatchesUploadToken(upload, properties))
            throw new FileStorageException(FileStorageError.OutcomeUnknown,
                "S3 分片完成后对象属性未能与会话核对（对象=" + (properties == null ? "missing" : properties.Length.ToString(CultureInfo.InvariantCulture)) +
                ", 元数据键=" + (properties == null ? "" : string.Join(",", properties.Metadata.Keys)) + "）。");
        return new FileWriteResult(new FileLocation(Name, Scope, upload.Request.Address), properties);
    }

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

    private bool MatchesUploadToken(MultipartUpload upload, FileProperties properties) =>
        upload.Request.Metadata.TryGetValue(UploadTokenMetadata, out var token) &&
        properties.Metadata.TryGetValue(UploadTokenMetadata, out var actual) && actual == token;

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

    private GetObjectRequest ObjectRequest(FileAddress address) => new()
    {
        BucketName = Bucket(address),
        Key = ObjectKey(address)
    };

    private string Bucket(FileAddress address)
    {
        var bucket = address.Container;
        if (_containerBuckets.Count != 0 && !_containerBuckets.TryGetValue(address.Container, out bucket))
            throw new FileStorageException(FileStorageError.Configuration, "逻辑容器没有配置 S3 空间映射。");
        ValidateBucket(bucket);
        return bucket;
    }

    private string ObjectKey(FileAddress address) => FileStorageStreams.ObjectKey(_storageNamespace, Scope, address);

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

    private static bool IsToken(string value) => value.Length == 32 && value.All(Uri.IsHexDigit);

    private static void ValidateMetadataSize(IReadOnlyDictionary<string, string> metadata)
    {
        // FileWriteRequest 已限定为 ASCII；S3 用户元数据键和值的合计上限为 2 KiB。
        if (metadata.Sum(p => p.Key.Length + p.Value.Length) > 2048)
            throw new ArgumentException("S3 元数据（包含 Provider 字段）不能超过 2 KiB。", nameof(metadata));
    }

    internal static void ValidateBucket(string bucket)
    {
        if (string.IsNullOrEmpty(bucket) || bucket.Length is < 3 or > 63 || IPAddress.TryParse(bucket, out _) || bucket.Contains("..", StringComparison.Ordinal) ||
            bucket.Contains(".-", StringComparison.Ordinal) || bucket.Contains("-.", StringComparison.Ordinal) ||
            bucket[0] == '.' || bucket[^1] == '.' || bucket[0] == '-' || bucket[^1] == '-' ||
            bucket.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '-')))
            throw new FileStorageException(FileStorageError.Configuration,
                "逻辑容器不能作为合法 S3 Bucket 名称使用；Bucket 名称须为 3 至 63 个小写 DNS 字符。");
    }

    private static bool IsMissingObject(AmazonS3Exception ex) =>
        ex.ErrorCode is "NoSuchKey" or "NotFound" || ex.StatusCode == HttpStatusCode.NotFound && ex.ErrorCode != "NoSuchBucket";

    private static bool IsNoSuchUpload(AmazonS3Exception ex) => ex.ErrorCode == "NoSuchUpload";

    private static void ValidateLifetime(TimeSpan lifetime)
    {
        if (lifetime <= TimeSpan.Zero || lifetime > MaxSignatureLifetime)
            throw new ArgumentOutOfRangeException(nameof(lifetime), "签名有效期必须大于 0 且不超过 7 天。");
    }

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

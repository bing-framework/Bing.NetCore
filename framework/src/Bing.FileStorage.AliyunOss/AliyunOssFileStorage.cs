using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using AlibabaCloud.OSS.V2;
using AlibabaCloud.OSS.V2.Models;

namespace Bing.FileStorage;

/// <summary>
/// 绑定操作范围的 OSS 文件存储门面。
/// </summary>
/// <remarks>
/// 绑定一个主机或租户范围的 OSS 对象存储门面。
/// </remarks>
internal sealed class AliyunOssFileStorage : IFileStorage, IMultipartFileStorage, IMultipartFilePartReader,
    ISignedFileRequestProvider, IFileRangeReader
{
    /// <summary>
    /// 定义阿里云 OSS 会话在对象元数据中使用的键。
    /// </summary>
    private const string OssSessionMetadata = "bing-oss-session";
    /// <summary>
    /// 定义分片上传会话在对象元数据中使用的键。
    /// </summary>
    private const string UploadSessionMetadata = "bing-upload-session";
    /// <summary>
    /// 定义阿里云 OSS 分片上传的最小分片大小，单位为字节。
    /// </summary>
    private const int MinPartSize = 5 * 1024 * 1024;
    /// <summary>
    /// 定义阿里云 OSS 分片上传的最大分片大小，单位为字节。
    /// </summary>
    private const int MaxPartSize = 128 * 1024 * 1024;
    /// <summary>
    /// 定义阿里云 OSS 分片上传的最大分片数量。
    /// </summary>
    private const int MaxPartCount = 10000;
    /// <summary>
    /// 定义阿里云 OSS 预签名地址的最长有效期。
    /// </summary>
    private static readonly TimeSpan MaxSignatureLifetime = TimeSpan.FromDays(7);

    /// <summary>
    /// 保存阿里云 OSS 存储使用的命名空间前缀。
    /// </summary>
    private readonly string _storageNamespace;
    /// <summary>
    /// 保存阿里云 OSS 客户端实例。
    /// </summary>
    private readonly Client _client;
    /// <summary>
    /// 保存列举分片时使用的分页大小。
    /// </summary>
    private readonly int _listPartsPageSize;
    /// <summary>
    /// 保存容器名称到 OSS 存储桶的映射。
    /// </summary>
    private readonly IReadOnlyDictionary<string, string> _containerBuckets;

    /// <summary>
    /// 初始化一个 <see cref="AliyunOssFileStorage" /> 类型的实例。
    /// </summary>
    /// <param name="name">实例使用的名称。</param>
    /// <param name="storageNamespace">文件对象使用的逻辑存储命名空间。</param>
    /// <param name="scope">实例所属的作用范围。</param>
    /// <param name="client">用于访问对应后端或服务的客户端。</param>
    /// <param name="listPartsPageSize">列出分片时每页读取的分片数量。</param>
    /// <param name="containerBuckets">逻辑容器名称到后端存储桶名称的映射。</param>
    public AliyunOssFileStorage(string name, string storageNamespace, StorageScope scope, Client client,
        int listPartsPageSize, IReadOnlyDictionary<string, string> containerBuckets)
    {
        Name = name;
        _storageNamespace = storageNamespace;
        Scope = scope;
        _client = client;
        _listPartsPageSize = listPartsPageSize;
        _containerBuckets = containerBuckets;
    }

    /// <inheritdoc />
    public string Name { get; }
    /// <inheritdoc />
    public StorageScope Scope { get; }
    /// <inheritdoc />
    public FileStorageCapabilities Capabilities => FileStorageCapabilities.Multipart |
        FileStorageCapabilities.SignedDownload | FileStorageCapabilities.Range |
        FileStorageCapabilities.ServerVerifiedParts;

    /// <inheritdoc />
    public async Task<FileWriteResult> SaveAsync(FileWriteRequest request, Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        FileStorageStreams.ValidateSource(source, request.Length);
        RejectCreateOnly(request.Mode);
        _ = Bucket(request.Address);
        ValidateCallerMetadata(request.Metadata);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Length <= MinPartSize)
        {
            await using var payload = await StageInputAsync(source, request.Length, cancellationToken).ConfigureAwait(false);
            return await PutObjectAsync(request, payload, Guid.NewGuid().ToString("N"), cancellationToken).ConfigureAwait(false);
        }

        var partSize = SelectPartSize(request.Length);
        var upload = await CreateMultipartAsync(request, partSize, cancellationToken).ConfigureAwait(false);
        try
        {
            var parts = new List<FilePart>(upload.PartCount);
            for (var partNumber = 1; partNumber <= upload.PartCount; partNumber++)
            {
                using var boundedSource = new BoundedReadStream(source, upload.GetPartLength(partNumber));
                parts.Add(await UploadPartAsync(upload, partNumber, boundedSource,
                    cancellationToken: cancellationToken).ConfigureAwait(false));
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
    public async Task ReadAsync(FileAddress address, Func<Stream, CancellationToken, Task> consume,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(consume);
        _ = Bucket(address);
        cancellationToken.ThrowIfCancellationRequested();

        GetObjectResult result;
        try
        {
            result = await _client.GetObjectAsync(ObjectRequest(address), HttpCompletionOption.ResponseHeadersRead,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsProviderFailure(ex))
        {
            throw MapFailure(ex, uncertain: false);
        }

        if (result.Body == null)
            throw new FileStorageException(FileStorageError.Unavailable, "OSS 未返回对象读取流。");
        using (result.Body)
            await consume(result.Body, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<FileProperties?> GetPropertiesAsync(FileAddress address,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        _ = Bucket(address);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var result = await _client.HeadObjectAsync(new HeadObjectRequest
            {
                Bucket = Bucket(address),
                Key = ObjectKey(address)
            }, cancellationToken: cancellationToken).ConfigureAwait(false);
            return ToProperties(result);
        }
        catch (Exception ex) when (IsProviderFailure(ex))
        {
            if (FindServiceException(ex) is { } service && IsMissingObject(service))
                return null;
            throw MapFailure(ex, uncertain: false);
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
                Bucket = Bucket(address),
                Key = ObjectKey(address)
            }, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsProviderFailure(ex))
        {
            throw MapFailure(ex, uncertain: true);
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
                throw new FileStorageException(FileStorageError.NotFound, "OSS 对象不存在。");
            if (offset > properties.Length)
                throw new FileStorageException(FileStorageError.InvalidLength, "Range 起点超出对象长度。");
            using var empty = new MemoryStream(Array.Empty<byte>(), writable: false);
            await consume(empty, cancellationToken).ConfigureAwait(false);
            return;
        }

        GetObjectResult result;
        try
        {
            result = await _client.GetObjectAsync(new GetObjectRequest
            {
                Bucket = Bucket(address),
                Key = ObjectKey(address),
                Range = string.Create(CultureInfo.InvariantCulture, $"bytes={offset}-{endExclusive - 1}"),
                RangeBehavior = "standard"
            }, HttpCompletionOption.ResponseHeadersRead, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsProviderFailure(ex))
        {
            throw MapFailure(ex, uncertain: false);
        }

        if (result.Body == null)
            throw new FileStorageException(FileStorageError.Unavailable, "OSS 未返回 Range 读取流。");
        using (result.Body)
        {
            if (!MatchesRange(result.ContentRange, offset, endExclusive - 1))
                throw new FileStorageException(FileStorageError.InvalidLength, "OSS 返回的 Range 与请求不一致。");
            using var bounded = new BoundedReadStream(result.Body, length);
            await consume(bounded, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<MultipartUpload> CreateMultipartAsync(FileWriteRequest request, int partSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RejectCreateOnly(request.Mode);
        if (request.Length == 0)
            throw new FileStorageException(FileStorageError.InvalidLength, "空文件必须通过普通对象写入提交。");
        _ = new MultipartUpload("validation", request, partSize);
        _ = Bucket(request.Address);
        ValidateCallerMetadata(request.Metadata);
        cancellationToken.ThrowIfCancellationRequested();

        var session = Guid.NewGuid().ToString("N");
        var objectMetadata = request.Metadata.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        objectMetadata.Add(OssSessionMetadata, session);
        ValidateMetadataSize(objectMetadata);

        var persistedMetadata = request.Metadata.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        persistedMetadata.Add(UploadSessionMetadata, session);
        var persistedRequest = FileWriteRequest.RestoreMultipart(request.Address, request.Length, request.Mode,
            request.ContentType, persistedMetadata);

        try
        {
            var result = await _client.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest
            {
                Bucket = Bucket(request.Address),
                Key = ObjectKey(request.Address),
                ContentType = request.ContentType,
                Metadata = objectMetadata
            }, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(result.UploadId))
                throw new FileStorageException(FileStorageError.OutcomeUnknown, "OSS 未返回分片上传标识。");
            return new MultipartUpload(result.UploadId, persistedRequest, partSize);
        }
        catch (FileStorageException)
        {
            throw;
        }
        catch (Exception ex) when (IsProviderFailure(ex))
        {
            throw MapFailure(ex, uncertain: true);
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
        EnsureUpload(upload);
        cancellationToken.ThrowIfCancellationRequested();

        await using var payload = await StageInputAsync(source, length, cancellationToken).ConfigureAwait(false);
        if (sha256 != null && !FixedEquals(sha256, payload.Sha256))
            throw new FileStorageException(FileStorageError.InvalidChecksum, "分片 SHA-256 与输入流不一致。");

        try
        {
            var result = await _client.UploadPartAsync(new UploadPartRequest
            {
                Bucket = Bucket(upload.Request.Address),
                Key = ObjectKey(upload.Request.Address),
                UploadId = upload.UploadId,
                PartNumber = partNumber,
                Body = payload.Stream,
                ContentLength = length,
                ContentMd5 = payload.Md5
            }, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(result.ETag) || string.IsNullOrWhiteSpace(result.HashCrc64))
                throw new FileStorageException(FileStorageError.InvalidChecksum, "OSS 分片回执缺少 ETag 或 CRC64。");
            return new FilePart { Number = partNumber, Length = length, ETag = result.ETag, Sha256 = payload.Sha256 };
        }
        catch (FileStorageException)
        {
            throw;
        }
        catch (Exception ex) when (IsProviderFailure(ex))
        {
            throw MapFailure(ex, uncertain: true);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FilePart>> ListPartsAsync(MultipartUpload upload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(upload);
        EnsureUpload(upload);
        var parts = new List<FilePart>();
        long? marker = null;
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
    /// <para>用 OSS 的排他分片游标只查询目标分片。</para>
    /// </remarks>
    public async Task<FilePart?> GetPartAsync(MultipartUpload upload, int partNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(upload);
        EnsureUpload(upload);
        upload.GetPartLength(partNumber);
        var page = await ReadPartPageAsync(upload, partNumber - 1, 1, cancellationToken).ConfigureAwait(false);
        var part = page.Parts.FirstOrDefault();
        return part?.Number == partNumber ? part : null;
    }

    /// <summary>
    /// 读取并验证 OSS 分片列表的一页。
    /// </summary>
    /// <param name="upload">包含 OSS 上传地址和上传标识的上传描述。</param>
    /// <param name="marker">分页起始标记；首次读取时为 null。</param>
    /// <param name="maxParts">本页允许返回的最大分片数。</param>
    /// <param name="cancellationToken">用于取消分片查询的令牌。</param>
    /// <returns>本页验证后的文件分片及下页起始标记；没有后续页时 NextMarker 为 null。</returns>
    /// <remarks>
    /// 按序验证 OSS 分片编号、长度和 ETag；返回后续页标记，没有后续页面时为 null。
    /// </remarks>
    private async Task<(IReadOnlyList<FilePart> Parts, long? NextMarker)> ReadPartPageAsync(
        MultipartUpload upload, long? marker, int maxParts, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await _client.ListPartsAsync(new ListPartsRequest
            {
                Bucket = Bucket(upload.Request.Address),
                Key = ObjectKey(upload.Request.Address),
                UploadId = upload.UploadId,
                PartNumberMarker = marker,
                MaxParts = maxParts
            }, cancellationToken: cancellationToken).ConfigureAwait(false);

            var details = page.Parts ?? new List<Part>();
            if (details.Count > maxParts)
                throw new FileStorageException(FileStorageError.InvalidPart, "OSS 分片页面超过请求的条数上限。");
            var parts = new List<FilePart>(details.Count);
            var previous = marker ?? 0;
            foreach (var part in details)
            {
                if (part.PartNumber is null or <= 0 or > MaxPartCount || part.PartNumber <= previous ||
                    part.Size is null or < 0 || string.IsNullOrWhiteSpace(part.ETag))
                    throw new FileStorageException(FileStorageError.InvalidPart, "OSS 返回了无效或无序的分片属性。");
                parts.Add(new FilePart
                {
                    Number = checked((int)part.PartNumber.Value),
                    Length = part.Size.Value,
                    ETag = part.ETag,
                    Sha256 = null
                });
                previous = part.PartNumber.Value;
            }

            if (page.IsTruncated != true)
                return (parts, null);
            var next = page.NextPartNumberMarker;
            if (parts.Count == 0 || next != previous || next <= (marker ?? 0))
                throw new FileStorageException(FileStorageError.OutcomeUnknown, "OSS 分片分页游标无效或未前进。");
            return (parts, next);
        }
        catch (FileStorageException)
        {
            throw;
        }
        catch (Exception ex) when (IsProviderFailure(ex))
        {
            throw MapFailure(ex, uncertain: false);
        }
    }

    /// <inheritdoc />
    public async Task<FileWriteResult> CompleteMultipartAsync(MultipartUpload upload, IReadOnlyList<FilePart> parts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(upload);
        ArgumentNullException.ThrowIfNull(parts);
        EnsureUpload(upload);
        FileStorageStreams.ValidateParts(upload, parts);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<FilePart> actualParts;
        try
        {
            actualParts = await ListPartsAsync(upload, cancellationToken).ConfigureAwait(false);
        }
        catch (FileStorageException ex) when (ex.Error == FileStorageError.UploadNotFound)
        {
            var completed = await GetPropertiesAsync(upload.Request.Address, cancellationToken).ConfigureAwait(false);
            if (MatchesCompletedUpload(upload, completed))
                return new FileWriteResult(new FileLocation(Name, Scope, upload.Request.Address), completed!);
            throw;
        }
        FileStorageStreams.ValidateParts(upload, actualParts);
        if (!PartsMatch(parts, actualParts))
            throw new FileStorageException(FileStorageError.InvalidPart, "提交的分片清单与 OSS 服务端清单不一致。");

        try
        {
            await _client.CompleteMultipartUploadAsync(new CompleteMultipartUploadRequest
            {
                Bucket = Bucket(upload.Request.Address),
                Key = ObjectKey(upload.Request.Address),
                UploadId = upload.UploadId,
                CompleteMultipartUpload = new CompleteMultipartUpload
                {
                    Parts = actualParts.Select(part => new UploadPart { PartNumber = part.Number, ETag = part.ETag }).ToList()
                }
            }, cancellationToken: cancellationToken).ConfigureAwait(false);

            return await ConfirmCompletedObjectAsync(upload, cancellationToken).ConfigureAwait(false);
        }
        catch (FileStorageException)
        {
            throw;
        }
        catch (Exception ex) when (IsProviderFailure(ex))
        {
            return await ReconcileCompleteFailureAsync(upload, ex, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task AbortMultipartAsync(MultipartUpload upload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(upload);
        EnsureUpload(upload);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await _client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest
            {
                Bucket = Bucket(upload.Request.Address),
                Key = ObjectKey(upload.Request.Address),
                UploadId = upload.UploadId
            }, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsProviderFailure(ex))
        {
            if (FindServiceException(ex) is { } service && IsNoSuchUpload(service))
                return;
            throw MapFailure(ex, uncertain: true);
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
        try
        {
            var request = ObjectRequest(address);
            if (fileName != null)
                request.ResponseContentDisposition = "attachment; filename*=UTF-8''" + Uri.EscapeDataString(fileName);
            var signed = _client.Presign(request, expiresAt.UtcDateTime);
            if (string.IsNullOrWhiteSpace(signed.Url) || string.IsNullOrWhiteSpace(signed.Method))
                throw new FileStorageException(FileStorageError.Unavailable, "OSS 未返回有效签名请求。");
            return Task.FromResult(new SignedFileRequest
            {
                Url = new Uri(signed.Url, UriKind.Absolute),
                Method = signed.Method,
                ExpiresAt = signed.Expiration ?? expiresAt,
                Headers = signed.SignedHeaders == null
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(signed.SignedHeaders, StringComparer.OrdinalIgnoreCase)
            });
        }
        catch (Exception ex) when (IsProviderFailure(ex))
        {
            throw MapFailure(ex, uncertain: false);
        }
    }

    /// <inheritdoc />
    public Task<SignedFileRequest> SignPartAsync(MultipartUpload upload, int partNumber, string sha256,
        TimeSpan lifetime, CancellationToken cancellationToken = default) =>
        throw new FileStorageException(FileStorageError.UnsupportedCapability, "OSS Provider 不签发分片直传请求。");

    /// <summary>
    /// 写入已暂存的 OSS 文件内容并验证服务端校验回执。
    /// </summary>
    /// <param name="request">文件地址、长度、内容类型及元数据请求。</param>
    /// <param name="payload">已暂存并计算校验摘要的文件数据。</param>
    /// <param name="session">用于核对写入结果的上传会话标识。</param>
    /// <param name="cancellationToken">用于取消 OSS 写入的令牌。</param>
    /// <returns>完成 OSS 单对象写入并通过服务端校验后的文件位置和属性。</returns>
    /// <remarks>
    /// 请求失败时映射为相应存储错误；服务端校验回执缺失时报告校验失败。
    /// </remarks>
    private async Task<FileWriteResult> PutObjectAsync(FileWriteRequest request, StagedPayload payload,
        string session, CancellationToken cancellationToken)
    {
        var metadata = request.Metadata.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        metadata.Add(OssSessionMetadata, session);
        ValidateMetadataSize(metadata);

        PutObjectResult result;
        try
        {
            result = await _client.PutObjectAsync(new PutObjectRequest
            {
                Bucket = Bucket(request.Address),
                Key = ObjectKey(request.Address),
                Body = payload.Stream,
                ContentLength = request.Length,
                ContentMd5 = payload.Md5,
                ContentType = request.ContentType,
                Metadata = metadata
            }, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsProviderFailure(ex))
        {
            throw MapFailure(ex, uncertain: true);
        }

        if (string.IsNullOrWhiteSpace(result.HashCrc64))
            throw new FileStorageException(FileStorageError.InvalidChecksum, "OSS 对象写入回执缺少 CRC64。");

        FileProperties? properties;
        try
        {
            properties = await GetPropertiesAsync(request.Address, cancellationToken).ConfigureAwait(false);
        }
        catch (FileStorageException ex) when (ex.Error is FileStorageError.AccessDenied or FileStorageError.Configuration)
        {
            throw;
        }
        catch (FileStorageException ex)
        {
            throw new FileStorageException(FileStorageError.OutcomeUnknown, "OSS 已返回写入回执，但属性查询失败，写入结果未知。", ex, ex.RequestId);
        }

        if (properties == null || properties.Length != request.Length)
            throw new FileStorageException(FileStorageError.OutcomeUnknown, "OSS 对象写入后无法核对对象长度。");
        if (!properties.Metadata.TryGetValue(OssSessionMetadata, out var actualSession) || actualSession != session)
        {
            if (properties.Metadata.ContainsKey(OssSessionMetadata))
                throw new FileStorageException(FileStorageError.Conflict, "OSS 对象位置已被其他上传会话占用。");
            throw new FileStorageException(FileStorageError.OutcomeUnknown, "OSS 对象写入后未能读取会话元数据。");
        }
        return new FileWriteResult(new FileLocation(Name, Scope, request.Address), properties);
    }

    /// <summary>
    /// 读取 OSS 对象属性并确认对象属于当前上传会话。
    /// </summary>
    /// <param name="upload">待确认的分片上传请求及会话标识。</param>
    /// <param name="cancellationToken">用于取消属性查询的令牌。</param>
    /// <returns>已确认属于当前上传会话的文件位置和对象属性。</returns>
    /// <remarks>
    /// 对象长度或会话元数据不匹配时抛出 OutcomeUnknown 文件存储异常。
    /// </remarks>
    private async Task<FileWriteResult> ConfirmCompletedObjectAsync(MultipartUpload upload,
        CancellationToken cancellationToken)
    {
        var properties = await GetPropertiesAsync(upload.Request.Address, cancellationToken).ConfigureAwait(false);
        if (!MatchesCompletedUpload(upload, properties))
            throw new FileStorageException(FileStorageError.OutcomeUnknown, "OSS 完成分片后对象属性无法与上传会话核对。");
        return new FileWriteResult(new FileLocation(Name, Scope, upload.Request.Address), properties!);
    }

    /// <summary>
    /// 在 OSS 完成分片请求失败后核对远端对象状态。
    /// </summary>
    /// <param name="upload">当前分片上传及预期对象信息。</param>
    /// <param name="failure">完成请求的原始失败。</param>
    /// <param name="cancellationToken">用于取消对象属性查询的令牌。</param>
    /// <returns>核对远端对象状态后确认的文件写入结果。</returns>
    /// <remarks>
    /// 若对象属性匹配当前上传会话则返回成功结果；无法确认时将原始失败映射为结果未知错误。
    /// </remarks>
    private async Task<FileWriteResult> ReconcileCompleteFailureAsync(MultipartUpload upload, Exception failure,
        CancellationToken cancellationToken)
    {
        try
        {
            var properties = await GetPropertiesAsync(upload.Request.Address, cancellationToken).ConfigureAwait(false);
            if (MatchesCompletedUpload(upload, properties))
                return new FileWriteResult(new FileLocation(Name, Scope, upload.Request.Address), properties!);
            if (properties?.Metadata.ContainsKey(OssSessionMetadata) == true)
                throw new FileStorageException(FileStorageError.Conflict, "OSS 对象位置已被其他上传会话占用。", failure);
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
        catch (FileStorageException)
        {
        }

        var mapped = MapFailure(failure, uncertain: true);
        if (mapped.Error is FileStorageError.UploadNotFound or FileStorageError.InvalidPart or FileStorageError.InvalidChecksum or
            FileStorageError.AccessDenied or FileStorageError.Configuration)
            throw mapped;
        throw new FileStorageException(FileStorageError.OutcomeUnknown, "OSS 完成结果未知，且对象尚未能与上传会话匹配。", failure,
            FindServiceException(failure)?.RequestId);
    }

    /// <summary>
    /// 将输入流暂存到临时文件并计算上传校验摘要。
    /// </summary>
    /// <param name="source">待暂存的输入流。</param>
    /// <param name="length">文件预期字节长度。</param>
    /// <param name="cancellationToken">用于取消读取和暂存的令牌。</param>
    /// <returns>包含已暂存数据流及其长度、MD5 和 SHA-256 摘要的载荷。</returns>
    /// <remarks>
    /// 使用临时文件流式暂存并计算 MD5 与 SHA-256；失败时释放未完成的暂存资源。
    /// </remarks>
    private async Task<StagedPayload> StageInputAsync(Stream source, long length, CancellationToken cancellationToken)
    {
        FileStorageStreams.ValidateSource(source, length);
        var path = Path.Combine(Path.GetTempPath(), "bing-oss-" + Guid.NewGuid().ToString("N") + ".tmp");
        FileStream staged;
        try
        {
            staged = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 128 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);
        }
        catch (IOException ex)
        {
            throw new FileStorageException(FileStorageError.CapacityExceeded, "无法在临时目录创建 OSS 上传暂存文件。", ex);
        }

        try
        {
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
            var buffer = new byte[128 * 1024];
            var remaining = length;
            while (remaining > 0)
            {
                var read = await source.ReadAsync(buffer, 0, (int)Math.Min(buffer.Length, remaining), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                    throw new FileStorageException(FileStorageError.InvalidLength, "输入流短于声明长度。");
                try
                {
                    await staged.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                }
                catch (IOException ex)
                {
                    throw new FileStorageException(FileStorageError.CapacityExceeded, "无法暂存 OSS 上传数据。", ex);
                }
                sha.AppendData(buffer, 0, read);
                md5.AppendData(buffer, 0, read);
                remaining -= read;
            }
            if (await source.ReadAsync(buffer, 0, 1, cancellationToken).ConfigureAwait(false) != 0)
                throw new FileStorageException(FileStorageError.InvalidLength, "输入流超过声明长度。");

            staged.Position = 0;
            return new StagedPayload(staged, Convert.ToBase64String(sha.GetHashAndReset()),
                Convert.ToBase64String(md5.GetHashAndReset()));
        }
        catch
        {
            await staged.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// 判断 OSS 对象属性是否匹配当前上传会话。
    /// </summary>
    /// <param name="upload">包含预期长度和会话元数据的上传请求。</param>
    /// <param name="properties">远端对象属性；对象不存在时为 null。</param>
    /// <returns>当对象长度及上传会话元数据均与已完成上传匹配时为 true；否则为 false。</returns>
    /// <remarks>
    /// 对象长度及会话标识均匹配时返回 true，否则返回 false。
    /// </remarks>
    private bool MatchesCompletedUpload(MultipartUpload upload, FileProperties? properties) =>
        properties != null && properties.Length == upload.Request.Length &&
        upload.Request.Metadata.TryGetValue(UploadSessionMetadata, out var session) &&
        properties.Metadata.TryGetValue(OssSessionMetadata, out var actualSession) &&
        string.Equals(session, actualSession, StringComparison.Ordinal);

    /// <summary>
    /// 逐项比较两组分片的序号、长度和 ETag。
    /// </summary>
    /// <param name="expected">上传计划中的预期分片集合。</param>
    /// <param name="actual">远端查询或完成回执中的实际分片集合。</param>
    /// <returns>当两组分片数量、顺序、编号、长度和 ETag 均相同时为 true；否则为 false。</returns>
    /// <remarks>
    /// 分片数及各项字段完全相同时返回 true，否则返回 false。
    /// </remarks>
    private static bool PartsMatch(IReadOnlyList<FilePart> expected, IReadOnlyList<FilePart> actual)
    {
        if (expected.Count != actual.Count)
            return false;
        for (var index = 0; index < expected.Count; index++)
            if (expected[index].Number != actual[index].Number || expected[index].Length != actual[index].Length ||
                !string.Equals(expected[index].ETag, actual[index].ETag, StringComparison.Ordinal))
                return false;
        return true;
    }

    /// <summary>
    /// 将 OSS HeadObject 回执转换为文件属性模型。
    /// </summary>
    /// <param name="result">OSS 返回的 HeadObject 属性。</param>
    /// <returns>由 OSS 对象元数据转换得到的文件属性。</returns>
    /// <remarks>
    /// 缺少有效对象长度时抛出 OutcomeUnknown 文件存储异常；未提供有效更新时间时使用 DateTimeOffset.MinValue。
    /// </remarks>
    private FileProperties ToProperties(HeadObjectResult result)
    {
        if (result.ContentLength is null || result.ContentLength < 0)
            throw new FileStorageException(FileStorageError.OutcomeUnknown, "OSS 未返回有效对象长度。");
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in result.Metadata ?? new Dictionary<string, string>())
        {
            var key = pair.Key.StartsWith("x-oss-meta-", StringComparison.OrdinalIgnoreCase)
                ? pair.Key["x-oss-meta-".Length..]
                : pair.Key;
            metadata[key.ToLowerInvariant()] = pair.Value;
        }
        var lastModified = DateTimeOffset.MinValue;
        if (result.LastModified != null && DateTimeOffset.TryParse(result.LastModified, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            lastModified = parsed;
        return new FileProperties
        {
            Length = result.ContentLength.Value,
            LastModifiedUtc = lastModified,
            ContentType = result.ContentType,
            ETag = result.ETag,
            VersionId = result.VersionId,
            Metadata = metadata
        };
    }

    /// <summary>
    /// 根据文件地址创建 OSS 对象读取请求。
    /// </summary>
    /// <param name="address">待读取对象的文件地址。</param>
    /// <returns>包含解析后 Bucket 和对象键的 OSS 读取请求。</returns>
    /// <remarks>
    /// 请求包含按配置解析的 OSS Bucket 和对象键。
    /// </remarks>
    private GetObjectRequest ObjectRequest(FileAddress address) => new()
    {
        Bucket = Bucket(address),
        Key = ObjectKey(address)
    };

    /// <summary>
    /// 将逻辑容器名称解析为 OSS Bucket 名称。
    /// </summary>
    /// <param name="address">包含逻辑容器的文件地址。</param>
    /// <returns>经容器映射解析并通过 Bucket 校验的名称。</returns>
    /// <remarks>
    /// 配置了逻辑容器映射时只接受已登记容器；未配置映射时使用地址中的容器名。
    /// </remarks>
    private string Bucket(FileAddress address)
    {
        var bucket = address.Container;
        if (_containerBuckets.Count != 0 && !_containerBuckets.TryGetValue(address.Container, out bucket))
            throw new FileStorageException(FileStorageError.Configuration, "逻辑容器没有配置 OSS Bucket 映射。");
        ValidateBucket(bucket);
        return bucket;
    }

    /// <summary>
    /// 为 OSS 文件地址生成带命名空间和作用域的对象键。
    /// </summary>
    /// <param name="address">要映射到 OSS 的文件地址。</param>
    /// <returns>由存储命名空间、作用域和文件地址生成的 OSS 对象键。</returns>
    private string ObjectKey(FileAddress address) => FileStorageStreams.ObjectKey(_storageNamespace, Scope, address);

    /// <summary>
    /// 验证分片上传请求及其 OSS 会话检查点。
    /// </summary>
    /// <param name="upload">待操作的分片上传状态。</param>
    /// <remarks>
    /// 检查容器与写入模式，并要求检查点包含此 Provider 创建的会话令牌。
    /// </remarks>
    private void EnsureUpload(MultipartUpload upload)
    {
        _ = Bucket(upload.Request.Address);
        RejectCreateOnly(upload.Request.Mode);
        if (!upload.Request.Metadata.TryGetValue(UploadSessionMetadata, out var token) ||
            !Guid.TryParseExact(token, "N", out _))
            throw new FileStorageException(FileStorageError.Conflict, "OSS 上传会话的 Provider 元数据无效。");
        if (upload.Request.Metadata.ContainsKey(OssSessionMetadata) ||
            upload.Request.Metadata.Keys.Any(key => FileWriteRequest.IsMultipartMetadata(key) && key != UploadSessionMetadata))
            throw new FileStorageException(FileStorageError.Conflict, "OSS 上传会话包含冲突的 Provider 元数据。");
        ValidateMetadataSize(upload.Request.Metadata);
    }

    /// <summary>
    /// 拒绝调用方使用 OSS Provider 保留的元数据键。
    /// </summary>
    /// <param name="metadata">待验证的调用方对象元数据。</param>
    /// <remarks>
    /// OSS 会话键及分片元数据由 Provider 管理，调用方不得自行提供。
    /// </remarks>
    private static void ValidateCallerMetadata(IReadOnlyDictionary<string, string> metadata)
    {
        if (metadata.ContainsKey(OssSessionMetadata) || metadata.Keys.Any(FileWriteRequest.IsMultipartMetadata))
            throw new ArgumentException("文件元数据不能使用 OSS Provider 保留键。", nameof(metadata));
    }

    /// <summary>
    /// 验证 OSS 用户元数据总长度限制。
    /// </summary>
    /// <param name="metadata">待写入的对象元数据。</param>
    /// <remarks>
    /// 键和值总字符数不得超过 8 KiB。
    /// </remarks>
    private static void ValidateMetadataSize(IReadOnlyDictionary<string, string> metadata)
    {
        if (metadata.Sum(pair => pair.Key.Length + pair.Value.Length) > 8 * 1024)
            throw new ArgumentException("OSS 用户元数据不能超过 8 KiB。", nameof(metadata));
    }

    /// <summary>
    /// 根据对象长度计算满足 OSS 限制的分片大小。
    /// </summary>
    /// <param name="length">待上传对象的总字节数。</param>
    /// <returns>按 OSS 分片数量和尺寸限制计算并向上对齐至 MiB 的分片大小，单位为字节。</returns>
    /// <remarks>
    /// 计算结果保证分片数量不超过 MaxPartCount，并按 Provider 支持的分片单位取整。
    /// </remarks>
    private static int SelectPartSize(long length)
    {
        var minimum = length / MaxPartCount + (length % MaxPartCount == 0 ? 0 : 1);
        var target = Math.Max((long)MinPartSize, minimum);
        var rounded = ((target - 1) / (1024 * 1024) + 1) * 1024 * 1024;
        if (rounded > MaxPartSize || length > rounded * MaxPartCount)
            throw new FileStorageException(FileStorageError.InvalidLength, "文件超过 OSS 分片上传容量限制。");
        return checked((int)rounded);
    }

    /// <summary>
    /// 验证字符串是否为合法的 OSS Bucket 名称。
    /// </summary>
    /// <param name="bucket">待验证的 Bucket 名称。</param>
    /// <remarks>
    /// 按 OSS 名称规则检查长度、字符类别和起止字符；不符合时抛出参数异常。
    /// </remarks>
    internal static void ValidateBucket(string bucket)
    {
        static bool IsAlphaNumeric(char value) => value is >= 'a' and <= 'z' or >= '0' and <= '9';
        if (string.IsNullOrEmpty(bucket) || bucket.Length is < 3 or > 63 || !IsAlphaNumeric(bucket[0]) ||
            !IsAlphaNumeric(bucket[^1]) ||
            bucket.Any(character => !(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')))
            throw new FileStorageException(FileStorageError.Configuration, "OSS Bucket 名称须为 3 至 63 位小写字母、数字或连字符。");
    }

    /// <summary>
    /// 检查 Content-Range 是否与预期字节区间一致。
    /// </summary>
    /// <param name="contentRange">响应中的 Content-Range 文本。</param>
    /// <param name="offset">预期区间的起始字节偏移。</param>
    /// <param name="inclusiveEnd">预期区间的末尾字节偏移，包含该位置。</param>
    /// <returns>当 Content-Range 的起始位置和结束位置匹配请求范围，且总长度大于结束位置时为 true；否则为 false。</returns>
    /// <remarks>
    /// 返回 true 表示内容范围格式及起止偏移匹配；否则返回 false。
    /// </remarks>
    private static bool MatchesRange(string? contentRange, long offset, long inclusiveEnd)
    {
        if (string.IsNullOrWhiteSpace(contentRange) || !contentRange.StartsWith("bytes ", StringComparison.OrdinalIgnoreCase))
            return false;
        var rangeAndTotal = contentRange[6..].Split('/', 2);
        if (rangeAndTotal.Length != 2)
            return false;
        var bounds = rangeAndTotal[0].Split('-', 2);
        if (bounds.Length != 2)
            bounds = rangeAndTotal[0].Split('~', 2);
        return bounds.Length == 2 &&
            long.TryParse(bounds[0], NumberStyles.None, CultureInfo.InvariantCulture, out var actualStart) &&
            long.TryParse(bounds[1], NumberStyles.None, CultureInfo.InvariantCulture, out var actualEnd) &&
            actualStart == offset && actualEnd == inclusiveEnd &&
            long.TryParse(rangeAndTotal[1], NumberStyles.None, CultureInfo.InvariantCulture, out var total) && total > actualEnd;
    }

    /// <summary>
    /// 拒绝 OSS 无法原子保证的仅创建写入模式。
    /// </summary>
    /// <param name="mode">文件写入模式。</param>
    /// <remarks>
    /// CreateOnly 模式不受当前 Provider 的原子条件写入保证，使用时抛出 UnsupportedCapability。
    /// </remarks>
    private static void RejectCreateOnly(FileWriteMode mode)
    {
        if (mode == FileWriteMode.CreateOnly)
            throw new FileStorageException(FileStorageError.UnsupportedCapability, "OSS Provider 不提供原子 CreateOnly 保证。");
    }

    /// <summary>
    /// 验证对象下载签名的有效时长。
    /// </summary>
    /// <param name="lifetime">签名链接的有效期。</param>
    /// <remarks>
    /// 时长必须大于零且不超过七天。
    /// </remarks>
    private static void ValidateLifetime(TimeSpan lifetime)
    {
        if (lifetime <= TimeSpan.Zero || lifetime > MaxSignatureLifetime)
            throw new ArgumentOutOfRangeException(nameof(lifetime), "签名有效期须大于 0 且不超过 7 天。");
    }

    /// <summary>
    /// 以定时比较方式检查两个 Base64 摘要是否相等。
    /// </summary>
    /// <param name="left">左侧 Base64 编码摘要。</param>
    /// <param name="right">右侧 Base64 编码摘要。</param>
    /// <returns>当两个 Base64 字符串解码后的字节序列等长且定时比较相等时为 true；格式无效或内容不同时为 false。</returns>
    /// <remarks>
    /// 摘要长度不同或编码无效时返回 false；比较字节内容时避免依赖首个不同字节的位置。
    /// </remarks>
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
    /// 判断 OSS 服务异常是否表示对象不存在。
    /// </summary>
    /// <param name="ex">OSS 返回的服务异常。</param>
    /// <returns>当异常表示 OSS 对象不存在且不是 Bucket 不存在时为 true；否则为 false。</returns>
    /// <remarks>
    /// 仅将 NoSuchKey 或非 NoSuchBucket 的 HTTP 404 识别为对象不存在。
    /// </remarks>
    private static bool IsMissingObject(ServiceException ex) =>
        ex.ErrorCode is "NoSuchKey" or "NotFound" || ex.StatusCode == (int)HttpStatusCode.NotFound && ex.ErrorCode != "NoSuchBucket";

    /// <summary>
    /// 判断 OSS 服务异常是否表示上传会话不存在。
    /// </summary>
    /// <param name="ex">OSS 返回的服务异常。</param>
    /// <returns>当 OSS 错误码为 NoSuchUpload 时为 true；否则为 false。</returns>
    /// <remarks>
    /// 错误码为 NoSuchUpload 时返回 true，否则返回 false。
    /// </remarks>
    private static bool IsNoSuchUpload(ServiceException ex) => ex.ErrorCode == "NoSuchUpload";

    /// <summary>
    /// 判断异常是否来自 OSS Provider 可映射的请求失败。
    /// </summary>
    /// <param name="ex">待判断的异常。</param>
    /// <returns>当异常属于此适配器识别的 OSS SDK、操作、完整性、HTTP 请求或 I/O 失败类型时为 true；否则为 false。</returns>
    /// <remarks>
    /// 网络、文件 I/O、OSS 服务及客户端 Provider 操作异常属于可处理失败。
    /// </remarks>
    private static bool IsProviderFailure(Exception ex) => ex is ServiceException or RequestFailedException or
        OperationException or InconsistentException or NoRetryableInconsistentException or HttpRequestException or IOException;

    /// <summary>
    /// 沿异常内部链查找 OSS 服务异常。
    /// </summary>
    /// <param name="ex">待检查的外层异常。</param>
    /// <returns>异常链中的 OSS ServiceException；未找到时返回 null。</returns>
    /// <remarks>
    /// 找到第一个 ServiceException 时返回该实例；未找到时返回 null。
    /// </remarks>
    private static ServiceException? FindServiceException(Exception ex)
    {
        for (Exception? current = ex; current != null; current = current.InnerException)
            if (current is ServiceException service)
                return service;
        return null;
    }

    /// <summary>
    /// 沿异常内部链查找 OSS 一致性异常。
    /// </summary>
    /// <param name="ex">待检查的外层异常。</param>
    /// <returns>异常链中的 InconsistentException 或 NoRetryableInconsistentException；未找到时返回 null。</returns>
    /// <remarks>
    /// 找到一致性或不可重试一致性异常时返回该实例；未找到时返回 null。
    /// </remarks>
    private static Exception? FindInconsistentException(Exception ex)
    {
        for (Exception? current = ex; current != null; current = current.InnerException)
            if (current is InconsistentException or NoRetryableInconsistentException)
                return current;
        return null;
    }

    /// <summary>
    /// 将 OSS 操作异常映射为文件存储异常。
    /// </summary>
    /// <param name="ex">OSS 操作期间捕获的原始异常。</param>
    /// <param name="uncertain">调用方是否无法确认远端操作结果。</param>
    /// <returns>根据 OSS 错误代码、状态码和结果不确定标记映射得到的文件存储异常。</returns>
    /// <remarks>
    /// 优先区分一致性校验失败、OSS 服务状态、访问拒绝及不确定写入结果。
    /// </remarks>
    private static FileStorageException MapFailure(Exception ex, bool uncertain)
    {
        if (FindInconsistentException(ex) != null)
            return new FileStorageException(FileStorageError.InvalidChecksum, "OSS 数据完整性校验失败。", ex);
        var service = FindServiceException(ex);
        if (service == null)
            return new FileStorageException(uncertain ? FileStorageError.OutcomeUnknown : FileStorageError.Unavailable,
                "OSS 请求失败。", ex);

        var mapped = service.ErrorCode switch
        {
            "NoSuchBucket" => FileStorageError.Configuration,
            "NoSuchKey" or "NotFound" => FileStorageError.NotFound,
            "NoSuchUpload" => FileStorageError.UploadNotFound,
            "AccessDenied" or "InvalidAccessKeyId" or "SignatureDoesNotMatch" => FileStorageError.AccessDenied,
            "InvalidBucketName" or "InvalidRequest" or "InvalidArgument" or "AuthorizationHeaderMalformed" => FileStorageError.Configuration,
            "InvalidPart" or "InvalidPartOrder" or "EntityTooSmall" => FileStorageError.InvalidPart,
            "BadDigest" or "InvalidDigest" => FileStorageError.InvalidChecksum,
            "Conflict" or "PreconditionFailed" => FileStorageError.Conflict,
            _ => (FileStorageError?)null
        };
        var error = mapped ?? service.StatusCode switch
        {
            401 or 403 => FileStorageError.AccessDenied,
            404 when service.ErrorCode != "NoSuchBucket" => FileStorageError.NotFound,
            409 or 412 => FileStorageError.Conflict,
            416 => FileStorageError.InvalidLength,
            400 => FileStorageError.Configuration,
            _ => uncertain ? FileStorageError.OutcomeUnknown : FileStorageError.Unavailable
        };
        return new FileStorageException(error, "OSS 请求失败：" + (service.ErrorCode ?? service.Message), ex, service.RequestId);
    }

    /// <summary>
    /// 计算摘要后的临时上传内容。
    /// </summary>
    private sealed class StagedPayload : IAsyncDisposable
    {
        /// <summary>
        /// 初始化一个 <see cref="StagedPayload" /> 类型的实例。
        /// </summary>
        /// <param name="stream">用于读取内容的流。</param>
        /// <param name="sha256">用于校验文件内容的 SHA-256 摘要文本。</param>
        /// <param name="md5">用于校验对象存储内容的 MD5 摘要文本。</param>
        public StagedPayload(FileStream stream, string sha256, string md5)
        {
            Stream = stream;
            Sha256 = sha256;
            Md5 = md5;
        }

        /// <summary>
        /// 获取暂存上传内容的文件流。
        /// </summary>
        public FileStream Stream { get; }
        /// <summary>
        /// 获取内容 SHA-256 摘要的 Base64 文本。
        /// </summary>
        public string Sha256 { get; }
        /// <summary>
        /// 获取内容 MD5 摘要的 Base64 文本。
        /// </summary>
        public string Md5 { get; }

        /// <inheritdoc />
        public ValueTask DisposeAsync() => Stream.DisposeAsync();
    }
}

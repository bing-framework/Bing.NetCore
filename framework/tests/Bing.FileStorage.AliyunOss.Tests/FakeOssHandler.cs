using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using AlibabaCloud.OSS.V2;
using AlibabaCloud.OSS.V2.Credentials;
using AlibabaCloud.OSS.V2.Transport;

namespace Bing.FileStorage.AliyunOss.Tests;

/// <summary>
/// 为FakeOssHandler测试提供 HTTP 请求处理替身。
/// </summary>
internal sealed class FakeOssHandler : HttpMessageHandler
{
    /// <summary>
    /// 保存模拟 OSS 处理器记录的对象。
    /// </summary>
    private readonly Dictionary<string, StoredObject> _objects = new(StringComparer.Ordinal);
    /// <summary>
    /// 保存模拟 OSS 处理器记录的分片上传状态。
    /// </summary>
    private readonly Dictionary<string, MultipartState> _uploads = new(StringComparer.Ordinal);

    /// <summary>
    /// 获取已记录的请求列表。
    /// </summary>
    public List<RecordedRequest> Requests { get; } = new();
    /// <summary>
    /// 获取或设置是否返回错误的上传 CRC。
    /// </summary>
    public bool ReturnInvalidUploadCrc { get; set; }
    /// <summary>
    /// 获取或设置是否模拟拒绝终止上传。
    /// </summary>
    public bool DenyAbort { get; set; }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content == null ? Array.Empty<byte>() : await request.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in request.Headers)
            headers[header.Key] = string.Join(",", header.Value);
        if (request.Content != null)
            foreach (var header in request.Content.Headers)
                headers[header.Key] = string.Join(",", header.Value);

        var requestUri = request.RequestUri ?? throw new InvalidOperationException("OSS request has no URI.");
        var (bucket, key) = ParsePath(requestUri.AbsolutePath);
        var query = ParseQuery(requestUri.Query);
        var record = new RecordedRequest(request.Method.Method, requestUri, bucket, key, query, headers, body);
        Requests.Add(record);
        return Dispatch(record);
    }

    /// <summary>
    /// 分派模拟 OSS 协议请求。
    /// </summary>
    /// <param name="request">当前待检查或分派的 HTTP 请求。</param>
    /// <returns>构造或分派得到的 HTTP 响应。</returns>
    private HttpResponseMessage Dispatch(RecordedRequest request)
    {
        var objectId = request.Bucket + "/" + request.Key;
        if (request.Method == "POST" && request.Query.ContainsKey("uploads"))
        {
            var uploadId = "upload-1";
            _uploads[uploadId] = new MultipartState(request.Bucket, request.Key, Metadata(request.Headers),
                request.Headers.GetValueOrDefault("Content-Type") ?? "application/octet-stream");
            return Xml(HttpStatusCode.OK,
                $"<InitiateMultipartUploadResult><Bucket>{Escape(request.Bucket)}</Bucket><Key>{Escape(request.Key)}</Key><UploadId>{uploadId}</UploadId></InitiateMultipartUploadResult>");
        }

        if (request.Method == "PUT" && request.Query.TryGetValue("partNumber", out var partNumberText))
        {
            if (!TryUpload(request, out var upload))
                return Error(HttpStatusCode.NotFound, "NoSuchUpload");
            var partNumber = int.Parse(partNumberText, CultureInfo.InvariantCulture);
            upload.Parts[partNumber] = request.Body;
            upload.ETags[partNumber] = "\"etag-" + partNumber.ToString(CultureInfo.InvariantCulture) + "\"";
            var crc = Crc64(request.Body);
            if (ReturnInvalidUploadCrc)
                crc = (ulong.Parse(crc, CultureInfo.InvariantCulture) ^ 1).ToString(CultureInfo.InvariantCulture);
            var response = Empty(HttpStatusCode.OK);
            response.Headers.TryAddWithoutValidation("ETag", upload.ETags[partNumber]);
            response.Headers.TryAddWithoutValidation("x-oss-hash-crc64ecma", crc);
            response.Headers.TryAddWithoutValidation("x-oss-request-id", "request-part");
            return response;
        }

        if (request.Method == "PUT")
        {
            _objects[objectId] = new StoredObject(request.Body, Metadata(request.Headers),
                request.Headers.GetValueOrDefault("Content-Type") ?? "application/octet-stream", "\"etag-object\"");
            var response = Empty(HttpStatusCode.OK);
            response.Headers.TryAddWithoutValidation("ETag", "\"etag-object\"");
            response.Headers.TryAddWithoutValidation("x-oss-hash-crc64ecma", Crc64(request.Body));
            return response;
        }

        if (request.Method == "GET" && request.Query.ContainsKey("uploadId"))
            return ListParts(request);

        if (request.Method == "POST" && request.Query.ContainsKey("uploadId"))
        {
            if (!TryUpload(request, out var upload))
                return Error(HttpStatusCode.NotFound, "NoSuchUpload");
            var listed = XElement.Parse(Encoding.UTF8.GetString(request.Body));
            var submitted = listed.Elements("Part").Select(node => new
            {
                Number = (int?)node.Element("PartNumber"),
                ETag = (string?)node.Element("ETag")
            }).ToArray();
            if (submitted.Length != upload.Parts.Count || submitted.Any(part => part.Number is null ||
                !upload.ETags.TryGetValue(part.Number.Value, out var etag) || etag != part.ETag))
                return Error(HttpStatusCode.BadRequest, "InvalidPart");
            var completed = upload.Parts.OrderBy(pair => pair.Key).SelectMany(pair => pair.Value).ToArray();
            _objects[objectId] = new StoredObject(completed, upload.Metadata, upload.ContentType, "\"etag-complete\"");
            _uploads.Remove(request.Query["uploadId"]);
            return Xml(HttpStatusCode.OK,
                $"<CompleteMultipartUploadResult><Bucket>{Escape(request.Bucket)}</Bucket><Key>{Escape(request.Key)}</Key><ETag>\"etag-complete\"</ETag></CompleteMultipartUploadResult>");
        }

        if (request.Method == "DELETE" && request.Query.ContainsKey("uploadId"))
        {
            if (DenyAbort)
                return Error(HttpStatusCode.Forbidden, "AccessDenied");
            if (!_uploads.Remove(request.Query["uploadId"]))
                return Error(HttpStatusCode.NotFound, "NoSuchUpload");
            return Empty(HttpStatusCode.NoContent);
        }

        if (request.Method == "HEAD")
            return _objects.TryGetValue(objectId, out var headObject) ? Head(headObject) : Error(HttpStatusCode.NotFound, "NoSuchKey");

        if (request.Method == "GET")
        {
            if (!_objects.TryGetValue(objectId, out var getObject))
                return Error(HttpStatusCode.NotFound, "NoSuchKey");
            if (request.Headers.TryGetValue("Range", out var range))
                return Range(getObject, range);
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(getObject.Bytes) };
            response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(getObject.ContentType);
            response.Headers.TryAddWithoutValidation("ETag", getObject.ETag);
            AddMetadata(response, getObject.Metadata);
            return response;
        }

        if (request.Method == "DELETE")
        {
            _objects.Remove(objectId);
            return Empty(HttpStatusCode.NoContent);
        }

        return Error(HttpStatusCode.BadRequest, "InvalidRequest");
    }

    /// <summary>
    /// 创建模拟分片列表响应。
    /// </summary>
    /// <param name="request">当前待检查或分派的 HTTP 请求。</param>
    /// <returns>构造或分派得到的 HTTP 响应。</returns>
    private HttpResponseMessage ListParts(RecordedRequest request)
    {
        if (!TryUpload(request, out var upload))
            return Error(HttpStatusCode.NotFound, "NoSuchUpload");
        var marker = int.TryParse(request.Query.GetValueOrDefault("part-number-marker"), out var parsedMarker) ? parsedMarker : 0;
        var max = int.TryParse(request.Query.GetValueOrDefault("max-parts"), out var parsedMax) ? parsedMax : 1000;
        var available = upload.Parts.Where(part => part.Key > marker).OrderBy(part => part.Key).ToArray();
        var selected = available.Take(max).ToArray();
        var truncated = selected.Length < available.Length;
        var nextMarker = selected.Length == 0 ? marker : selected[^1].Key;
        var parts = string.Join("", selected.Select(part => $"<Part><PartNumber>{part.Key}</PartNumber><LastModified>2026-10-28T00:00:00.000Z</LastModified><ETag>{Escape(upload.ETags[part.Key])}</ETag><Size>{part.Value.Length}</Size><HashCrc64ecma>{Crc64(part.Value)}</HashCrc64ecma></Part>"));
        return Xml(HttpStatusCode.OK,
            $"<ListPartsResult><Bucket>{Escape(request.Bucket)}</Bucket><Key>{Escape(request.Key)}</Key><UploadId>{Escape(request.Query["uploadId"])}</UploadId><PartNumberMarker>{marker}</PartNumberMarker><NextPartNumberMarker>{nextMarker}</NextPartNumberMarker><MaxParts>{max}</MaxParts><IsTruncated>{truncated.ToString().ToLowerInvariant()}</IsTruncated>{parts}</ListPartsResult>");
    }

    /// <summary>
    /// 创建存储对象的元数据响应。
    /// </summary>
    /// <param name="value">模拟存储对象及其内容。</param>
    /// <returns>构造或分派得到的 HTTP 响应。</returns>
    private static HttpResponseMessage Head(StoredObject value)
    {
        var response = Empty(HttpStatusCode.OK);
        response.Content.Headers.ContentLength = value.Bytes.Length;
        response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(value.ContentType);
        response.Headers.TryAddWithoutValidation("ETag", value.ETag);
        response.Headers.TryAddWithoutValidation("Last-Modified", "Wed, 21 Oct 2015 07:28:00 GMT");
        AddMetadata(response, value.Metadata);
        return response;
    }

    /// <summary>
    /// 创建存储对象的范围读取响应。
    /// </summary>
    /// <param name="value">模拟存储对象及其内容。</param>
    /// <param name="header">HTTP 范围或测试请求头的文本值。</param>
    /// <returns>构造或分派得到的 HTTP 响应。</returns>
    private static HttpResponseMessage Range(StoredObject value, string header)
    {
        var bounds = header.Replace("bytes=", "", StringComparison.OrdinalIgnoreCase).Split('-', 2);
        var start = long.Parse(bounds[0], CultureInfo.InvariantCulture);
        var end = long.Parse(bounds[1], CultureInfo.InvariantCulture);
        if (start >= value.Bytes.Length || end >= value.Bytes.Length)
            return Error(HttpStatusCode.RequestedRangeNotSatisfiable, "InvalidRange");
        var bytes = value.Bytes.Skip((int)start).Take((int)(end - start + 1)).ToArray();
        var response = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(bytes) };
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(start, end, value.Bytes.Length);
        response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(value.ContentType);
        response.Headers.TryAddWithoutValidation("ETag", value.ETag);
        return response;
    }

    /// <summary>
    /// 查找并校验请求对应的分片上传会话。
    /// </summary>
    /// <param name="request">当前待检查或分派的 HTTP 请求。</param>
    /// <param name="upload">找到的分片上传会话输出值。</param>
    /// <returns>找到且匹配存储桶及对象键的上传会话时为 true，否则为 false。</returns>
    private bool TryUpload(RecordedRequest request, out MultipartState upload)
    {
        if (request.Query.TryGetValue("uploadId", out var uploadId) && _uploads.TryGetValue(uploadId, out upload!) &&
            upload.Bucket == request.Bucket && upload.Key == request.Key)
            return true;
        upload = null!;
        return false;
    }

    /// <summary>
    /// 从 OSS 请求头提取自定义元数据。
    /// </summary>
    /// <param name="headers">HTTP 请求头集合。</param>
    /// <returns>解析或构造的键值集合。</returns>
    private static Dictionary<string, string> Metadata(IReadOnlyDictionary<string, string> headers) => headers
        .Where(pair => pair.Key.StartsWith("x-oss-meta-", StringComparison.OrdinalIgnoreCase))
        .ToDictionary(pair => pair.Key["x-oss-meta-".Length..].ToLowerInvariant(), pair => pair.Value, StringComparer.Ordinal);

    /// <summary>
    /// 将自定义元数据写入 OSS 响应头。
    /// </summary>
    /// <param name="response">需要解析或写出的测试响应。</param>
    /// <param name="metadata">待写入或校验的文件元数据。</param>
    private static void AddMetadata(HttpResponseMessage response, IReadOnlyDictionary<string, string> metadata)
    {
        foreach (var pair in metadata)
            response.Headers.TryAddWithoutValidation("x-oss-meta-" + pair.Key, pair.Value);
    }

    /// <summary>
    /// 创建模拟 OSS 错误响应。
    /// </summary>
    /// <param name="code">HTTP 响应状态码。</param>
    /// <param name="errorCode">模拟存储服务返回的错误代码。</param>
    /// <returns>构造或分派得到的 HTTP 响应。</returns>
    private static HttpResponseMessage Error(HttpStatusCode code, string errorCode) =>
        Xml(code, $"<Error><Code>{errorCode}</Code><Message>{errorCode}</Message><RequestId>request-error</RequestId></Error>");

    /// <summary>
    /// 创建 OSS XML 响应。
    /// </summary>
    /// <param name="code">HTTP 响应状态码。</param>
    /// <param name="xml">模拟 OSS 响应的 XML 正文。</param>
    /// <returns>构造或分派得到的 HTTP 响应。</returns>
    private static HttpResponseMessage Xml(HttpStatusCode code, string xml)
    {
        var response = new HttpResponseMessage(code) { Content = new StringContent(xml, Encoding.UTF8, "application/xml") };
        response.Headers.TryAddWithoutValidation("x-oss-request-id", "request-test");
        return response;
    }

    /// <summary>
    /// 创建不含正文的 HTTP 响应。
    /// </summary>
    /// <param name="code">HTTP 响应状态码。</param>
    /// <returns>构造或分派得到的 HTTP 响应。</returns>
    private static HttpResponseMessage Empty(HttpStatusCode code) =>
        new(code) { Content = new ByteArrayContent(Array.Empty<byte>()) };

    /// <summary>
    /// 从请求路径解析存储桶及对象键。
    /// </summary>
    /// <param name="path">包含存储桶及对象键的 HTTP 请求路径。</param>
    /// <returns>从路径解码得到的存储桶和对象键。</returns>
    private static (string Bucket, string Key) ParsePath(string path)
    {
        var segments = Uri.UnescapeDataString(path.TrimStart('/')).Split('/', 2);
        return (segments[0], segments.Length == 1 ? "" : segments[1]);
    }

    /// <summary>
    /// 解析请求查询参数。
    /// </summary>
    /// <param name="query">待解析的 URL 查询文本。</param>
    /// <returns>解析或构造的键值集合。</returns>
    private static Dictionary<string, string> ParseQuery(string query)
    {
        var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = segment.IndexOf('=');
            var key = separator < 0 ? segment : segment[..separator];
            var value = separator < 0 ? "" : segment[(separator + 1)..];
            parsed[Uri.UnescapeDataString(key.Replace('+', ' '))] = Uri.UnescapeDataString(value.Replace('+', ' '));
        }
        return parsed;
    }

    /// <summary>
    /// 计算数据的 CRC64 校验值。
    /// </summary>
    /// <param name="value">需要计算校验值的字节内容。</param>
    /// <returns>CRC64 校验值的十进制文本。</returns>
    private static string Crc64(byte[] value)
    {
        const ulong polynomial = 0xC96C5795D7870F42;
        ulong crc = ulong.MaxValue;
        foreach (var item in value)
        {
            crc ^= item;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ polynomial : crc >> 1;
        }
        return (crc ^ ulong.MaxValue).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 转义 XML 文本中的特殊字符。
    /// </summary>
    /// <param name="value">测试对象、记录或输入文本的值。</param>
    /// <returns>适合放入 XML 的转义文本；转义结果为空时使用空字符串。</returns>
    private static string Escape(string value) => System.Security.SecurityElement.Escape(value) ?? "";

    /// <summary>
    /// 创建使用指定处理器的 OSS 测试客户端。
    /// </summary>
    /// <param name="handler">执行测试操作的处理器。</param>
    /// <returns>使用模拟端点及指定处理器的 OSS 客户端。</returns>
    public static Client CreateClient(HttpMessageHandler handler) => new(new Configuration
    {
        Region = "cn-hangzhou",
        Endpoint = "http://oss.test",
        UsePathStyle = true,
        SignatureVersion = "v4",
        RetryMaxAttempts = 1,
        DisableUploadCrc64Check = false,
        CredentialsProvider = new StaticCredentialsProvider("test-access", "test-secret"),
        HttpTransport = new HttpTransport(handler)
    });

    /// <summary>
    /// 记录对象存储协议测试中收到的 HTTP 请求内容。
    /// </summary>
    /// <param name="Method">HTTP 请求方法。</param>
    /// <param name="Uri">完整请求 URI。</param>
    /// <param name="Bucket">存储桶名称。</param>
    /// <param name="Key">租户键。</param>
    /// <param name="Query">请求查询参数集合。</param>
    /// <param name="Headers">请求头集合。</param>
    /// <param name="Body">请求正文。</param>
    internal sealed record RecordedRequest(string Method, Uri Uri, string Bucket, string Key,
        IReadOnlyDictionary<string, string> Query, IReadOnlyDictionary<string, string> Headers, byte[] Body);

    /// <summary>
    /// 保存测试对象的字节、元数据、媒体类型和 ETag。
    /// </summary>
    /// <param name="Bytes">用于初始化当前实例的Bytes值。</param>
    /// <param name="Metadata">用于初始化当前实例的Metadata值。</param>
    /// <param name="ContentType">用于初始化当前实例的ContentType值。</param>
    /// <param name="ETag">用于初始化当前实例的ETag值。</param>
    private sealed record StoredObject(byte[] Bytes, Dictionary<string, string> Metadata, string ContentType, string ETag);

    /// <summary>
    /// 保存 Fake OSS 分片上传的桶、对象键和分片状态。
    /// </summary>
    private sealed class MultipartState
    {
        /// <summary>
        /// 初始化一个 <see cref="MultipartState" /> 类型的实例。
        /// </summary>
        /// <param name="bucket">后端存储桶名称。</param>
        /// <param name="key">对象在容器内的键。</param>
        /// <param name="metadata">随对象保存的元数据。</param>
        /// <param name="contentType">内容的媒体类型；未指定时使用默认类型。</param>
        public MultipartState(string bucket, string key, Dictionary<string, string> metadata, string contentType)
        {
            Bucket = bucket;
            Key = key;
            Metadata = metadata;
            ContentType = contentType;
        }

        /// <summary>
        /// 获取文件存储使用的桶名称。
        /// </summary>
        public string Bucket { get; }
        /// <summary>
        /// 获取分片上传对应的对象键。
        /// </summary>
        public string Key { get; }
        /// <summary>
        /// 获取分片上传附带的文件元数据。
        /// </summary>
        public Dictionary<string, string> Metadata { get; }
        /// <summary>
        /// 获取文件内容的媒体类型。
        /// </summary>
        public string ContentType { get; }
        /// <summary>
        /// 获取已记录的文件分片。
        /// </summary>
        public SortedDictionary<int, byte[]> Parts { get; } = new();
        /// <summary>
        /// 获取已记录的对象或分片版本标记。
        /// </summary>
        public Dictionary<int, string> ETags { get; } = new();
    }
}

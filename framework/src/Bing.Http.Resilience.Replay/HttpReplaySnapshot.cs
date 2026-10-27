using Bing.Resilience;
using Bing.Resilience.Operations;
using System.Net.Http.Headers;

namespace Bing.Http.Resilience.Replay;

/// <summary>冻结请求字节、头与选项，每次发送（包括首发）创建全新请求和内容。</summary>
public sealed class HttpReplaySnapshot : IBingHttpReplaySnapshot
{
    private readonly HttpMethod _method;
    private readonly Uri _uri;
    private readonly Version _version;
    private readonly HttpVersionPolicy _versionPolicy;
    private readonly byte[]? _body;
    private readonly IHttpReplayPayload? _payload;
    private readonly KeyValuePair<string, string[]>[] _headers, _contentHeaders;
    private readonly KeyValuePair<string, object?>[] _values;
    private readonly HttpWriteReplayOptions _options;
    private readonly bool _shared;
    private int _disposed;
    public string BodyFingerprint { get; }
    internal HttpReplaySnapshot(HttpRequestMessage request, HttpWriteReplayOptions options, HttpIdempotencyContract contract,
        byte[]? body, IHttpReplayPayload? payload, string key)
    {
        _method = request.Method; _uri = request.RequestUri!; _version = request.Version; _versionPolicy = request.VersionPolicy;
        _options = options; _body = body; _payload = payload; _shared = contract.SharedAcrossEndpoints;
        BodyFingerprint = payload?.Fingerprint ?? OperationFingerprint.Hash(body ?? Array.Empty<byte>());
        _headers = request.Headers.Where(x => options.ForwardHeaders.Contains(x.Key) && !options.Forbidden(x.Key) &&
            !x.Key.Equals(options.IdempotencyHeader, StringComparison.OrdinalIgnoreCase))
            .Select(x => new KeyValuePair<string, string[]>(x.Key, x.Value.ToArray()))
            .Append(new(options.IdempotencyHeader, new[] { key })).ToArray();
        _contentHeaders = request.Content?.Headers.Where(x => !x.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            .Select(x => new KeyValuePair<string, string[]>(x.Key, x.Value.ToArray())).ToArray() ?? Array.Empty<KeyValuePair<string, string[]>>();
        _values = request.Options.ToArray();
        foreach (var pair in _values)
            if (!Immutable(pair.Value) && !options.OptionCopiers.ContainsKey(pair.Key))
                throw new ResilienceConfigurationException($"写重放 Options '{pair.Key}' 需要复制器。");
    }
    private static bool Immutable(object? value) => value is null or string or CancellationToken or Guid or decimal or DateTime or DateTimeOffset or
        TimeSpan or ResilienceOperationDescriptor || value.GetType().IsPrimitive || value.GetType().IsEnum;
    public async ValueTask<HttpRequestMessage> CreateRequestAsync(Uri? origin, CancellationToken token)
    {
        if (Volatile.Read(ref _disposed) != 0) throw new ObjectDisposedException(nameof(HttpReplaySnapshot));
        token.ThrowIfCancellationRequested();
        if (origin is not null && !_shared && !string.Equals(origin.GetLeftPart(UriPartial.Authority), _uri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
            throw new ResilienceConfigurationException("接收端未声明共享幂等域，不能跨实例写重放。");
        var uri = origin is null ? _uri : new Uri(origin.GetLeftPart(UriPartial.Authority) + _uri.PathAndQuery);
        var request = new HttpRequestMessage(_method, uri) { Version = _version, VersionPolicy = _versionPolicy };
        try
        {
            if (_payload is not null)
            {
                var stream = await _payload.OpenReadAsync(token).ConfigureAwait(false);
                if (!stream.CanRead) { stream.Dispose(); throw new ResilienceConfigurationException("重放载荷不可读。"); }
                request.Content = new StreamContent(stream);
                request.Content.Headers.ContentLength = _payload.Length;
            }
            else if (_body is not null) request.Content = new ByteArrayContent(_body);
            if (request.Content is not null) foreach (var h in _contentHeaders) request.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
            foreach (var h in _headers) request.Headers.TryAddWithoutValidation(h.Key, h.Value);
            foreach (var pair in _values)
                request.Options.Set(new HttpRequestOptionsKey<object?>(pair.Key), _options.OptionCopiers.TryGetValue(pair.Key, out var copy) ? copy(pair.Value) : pair.Value);
            return request;
        }
        catch { request.Dispose(); throw; }
    }
    public void Dispose() => Interlocked.Exchange(ref _disposed, 1);
}

internal sealed class HttpWriteReplayStrategy(string clientName, HttpWriteReplayOptions options, ResilienceOperationCatalog catalog) : IBingHttpReplayStrategy
{
    public string ClientName => clientName;
    public async ValueTask<IBingHttpReplaySnapshot?> CaptureAsync(HttpRequestMessage request, CancellationToken token)
    {
        if (!options.Enabled || ResilienceDurableAttemptScope.Current is not null || request.Method == HttpMethod.Get || request.Method == HttpMethod.Head ||
            request.Options.TryGetValue(BingHttpOperationMetadata.DurableAttempt, out var durable) && durable ||
            request.Options.TryGetValue(BingHttpOperationMetadata.IsStreamResult, out var stream) && stream ||
            request.Options.TryGetValue(BingHttpOperationMetadata.IsDefinition, out var definition) && definition ||
            !request.Options.TryGetValue(BingHttpOperationMetadata.Operation, out var declared) ||
            options.CanReplayWrite?.Invoke(request) != true) return null;
        var operation = catalog.Get(declared.Name);
        if (operation != declared || operation.Kind != ResilienceOperationKind.IdempotentWrite)
            throw new ResilienceConfigurationException("请求操作与宿主写许可不一致。");
        var contract = options.GetContract!(operation);
        if (contract is null || contract.Version != operation.ContractVersion || contract.Scope != operation.IdempotencyScope ||
            contract.Retention < options.ReplayWindow)
            throw new ResilienceConfigurationException("接收端幂等协议或保留期限不满足写重放契约。");
        if (!request.Options.TryGetValue(BingHttpOperationMetadata.IdempotencyKey, out var key) || string.IsNullOrWhiteSpace(key) || key.Length > 256 ||
            key.Any(char.IsControl) || !request.Options.TryGetValue(BingHttpOperationMetadata.AccessScope, out var scope) || string.IsNullOrWhiteSpace(scope))
            throw new ResilienceConfigurationException("写重放需要可信访问范围与稳定幂等键。");
        if (request.RequestUri is not { IsAbsoluteUri: true }) throw new ResilienceConfigurationException("重放需要绝对请求地址。");
        var payload = options.GetPayload?.Invoke(request);
        byte[]? body = null;
        if (payload is not null)
        {
            if (payload.Length < 0 || payload.Length > options.MaxPayloadBytes || payload.Fingerprint.Length != 64 || !payload.Fingerprint.All(Uri.IsHexDigit))
                throw new ResilienceConfigurationException("不可变载荷大小或指纹无效。");
        }
        else if (request.Content is not null)
        {
            if (request.Content is not ByteArrayContent && options.CanBufferContent?.Invoke(request) != true)
            {
                if (options.RequireReplayableBody) throw new ResilienceConfigurationException("未登记可重建请求体，首发前拒绝重放。");
                return null;
            }
            if (request.Content.Headers.ContentLength > options.MaxBufferedBytes) throw new ResilienceConfigurationException("请求体超过内存快照上限。");
            using var output = new MemoryStream();
            // 内容流属于原请求；此处读取但不释放，原请求所有者仍负责生命周期。
            var input = await request.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            var buffer = new byte[Math.Min(81920, options.MaxBufferedBytes + 1)];
            int length;
            while ((length = await input.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false)) != 0)
            {
                if (output.Length + length > options.MaxBufferedBytes) throw new ResilienceConfigurationException("请求体超过内存快照上限。");
                await output.WriteAsync(buffer.AsMemory(0, length), token).ConfigureAwait(false);
            }
            body = output.ToArray();
        }
        return new HttpReplaySnapshot(request, options, contract, body, payload, key);
    }
}

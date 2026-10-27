using System.Collections.Concurrent;
using System.Text.Json;

namespace Bing.Resilience.Discovery;

/// <summary>Manifest 独立于动态 API 方法描述，不携带执行代码或写入许可。</summary>
public sealed record EndpointManifest(int SchemaVersion, string ServiceName, string Version, long Revision, DateTimeOffset ValidUntil,
    string ConsistencyGroup, string ContractRevision, string Digest, IReadOnlyList<ServiceEndpoint> Endpoints);
public interface IEndpointManifestSource
{
    ValueTask<EndpointManifest> GetAsync(string serviceName, CancellationToken cancellationToken = default);
}
public sealed class HttpEndpointManifestOptions
{
    public HttpEndpointDirectoryOptions Directory { get; } = new();
    public long MinimumRevision { get; set; }
    public string ConsistencyGroup { get; set; } = "";
    public string ContractRevision { get; set; } = "";
    public Func<ReadOnlyMemory<byte>, bool>? VerifySignature { get; set; }
}

/// <summary>复用受控 HTTP 目录的 ETag、刷新、退避与大小限制；额外验证 Manifest 格式、签名、有效期和单调版本。</summary>
public sealed class HttpEndpointManifestSource : IEndpointManifestSource, IDisposable
{
    private readonly HttpDirectoryServiceEndpointSource _source;
    private readonly ConcurrentDictionary<string, EndpointManifest> _documents = new(StringComparer.Ordinal);
    private readonly TimeProvider _clock;
    private readonly object _gate = new();
    private long _revision;
    private string? _currentVersion;
    public HttpEndpointManifestSource(HttpEndpointManifestOptions options, HttpMessageHandler transport, IServiceEndpointTrustPolicy trust,
        TimeProvider clock, bool disposeTransport = false)
    {
        if (options.MinimumRevision < 0 || string.IsNullOrWhiteSpace(options.ConsistencyGroup) ||
            string.IsNullOrWhiteSpace(options.ContractRevision) || options.VerifySignature is null)
            throw new ResilienceConfigurationException("Manifest 必须配置版本下限、契约和签名验证。");
        _clock = clock; _revision = options.MinimumRevision;
        var group = options.ConsistencyGroup; var contract = options.ContractRevision; var verify = options.VerifySignature;
        var original = options.Directory;
        var directory = new HttpEndpointDirectoryOptions
        {
            ServiceName = original.ServiceName, DirectoryUri = original.DirectoryUri, TrustDirectory = original.TrustDirectory,
            ValidateCustomTransport = original.ValidateCustomTransport, PrepareRequestAsync = original.PrepareRequestAsync,
            MaximumBytes = original.MaximumBytes, MaximumEndpoints = original.MaximumEndpoints, RefreshPeriod = original.RefreshPeriod,
            RequestTimeout = original.RequestTimeout, TimeToLive = original.TimeToLive, MaxStale = original.MaxStale, FailureBackoff = original.FailureBackoff
        };
        directory.VerifyDocument = bytes =>
        {
            if (!verify(bytes)) return false;
            var value = JsonSerializer.Deserialize<EndpointManifest>(bytes.Span, new JsonSerializerOptions { PropertyNameCaseInsensitive = true, MaxDepth = 32 });
            if (value is null || value.SchemaVersion != 1 || value.ServiceName != directory.ServiceName ||
                string.IsNullOrWhiteSpace(value.Version) || value.Version.Length > 128 || value.ValidUntil <= clock.GetUtcNow() ||
                value.ValidUntil - clock.GetUtcNow() > TimeSpan.FromDays(1) || value.ConsistencyGroup != group || value.ContractRevision != contract ||
                value.Digest.Length != 64 || !value.Digest.All(Uri.IsHexDigit) || value.Endpoints is null || value.Endpoints.Count > directory.MaximumEndpoints ||
                value.Endpoints.Any(e => e.ConsistencyGroup != group || e.ContractRevision != contract || !trust.IsTrusted(value.ServiceName, e)))
                return false;
            lock (_gate)
            {
                if (value.Revision < _revision || value.Revision == _revision && _currentVersion is not null && value.Version != _currentVersion) return false;
                if (_documents.TryGetValue(value.Version, out var previous) &&
                    (previous.Revision != value.Revision || previous.Digest != value.Digest ||
                     !previous.Endpoints.SequenceEqual(value.Endpoints))) return false;
                foreach (var old in _documents.Where(p => p.Value.ValidUntil <= clock.GetUtcNow()).ToArray()) _documents.TryRemove(old.Key, out _);
                if (!_documents.ContainsKey(value.Version) && _documents.Count >= 128) return false;
                _revision = value.Revision; _currentVersion = value.Version;
                _documents[value.Version] = value with { Endpoints = Array.AsReadOnly(value.Endpoints.ToArray()) };
                return true;
            }
        };
        _source = new(directory, transport, trust, clock, disposeTransport);
    }
    public async ValueTask<EndpointManifest> GetAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        var snapshot = await _source.GetSnapshotAsync(serviceName, cancellationToken).ConfigureAwait(false);
        if (!_documents.TryGetValue(snapshot.SourceVersion, out var value) || value.ValidUntil <= _clock.GetUtcNow())
            throw new ResilienceDiscoveryException(serviceName, "Manifest 已过期或不可用。");
        return value;
    }
    public void Dispose() => _source.Dispose();
}

/// <summary>候选为可信发现与已验证 Manifest 的交集，不从 Manifest 获得新操作许可。</summary>
public sealed class ManifestServiceEndpointSource(IServiceEndpointSource discovery, IEndpointManifestSource manifests,
    IServiceEndpointTrustPolicy trust, TimeProvider clock) : IServiceEndpointSource
{
    public async ValueTask<ServiceEndpointSnapshot> GetSnapshotAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        var discoveryTask = discovery.GetSnapshotAsync(serviceName, cancellationToken).AsTask();
        var manifestTask = manifests.GetAsync(serviceName, cancellationToken).AsTask();
        await Task.WhenAll(discoveryTask, manifestTask).ConfigureAwait(false);
        var snapshot = await discoveryTask.ConfigureAwait(false); var manifest = await manifestTask.ConfigureAwait(false);
        if (manifest.ValidUntil <= clock.GetUtcNow()) throw new ResilienceDiscoveryException(serviceName, "Manifest 已过期。");
        var selected = snapshot.Endpoints.Where(e => trust.IsTrusted(serviceName, e) && !discovery.IsRevoked(serviceName, e) &&
            manifest.Endpoints.Any(m => m.StateKey == e.StateKey && m.Origin == e.Origin && m.ContractRevision == e.ContractRevision &&
                m.ConsistencyGroup == e.ConsistencyGroup && m.CredentialAudience == e.CredentialAudience)).ToArray();
        return new(serviceName, snapshot.SourceVersion + ":" + manifest.Revision, snapshot.FetchedAt,
            snapshot.ExpiresAt < manifest.ValidUntil ? snapshot.ExpiresAt : manifest.ValidUntil,
            snapshot.MaxStaleUntil < manifest.ValidUntil ? snapshot.MaxStaleUntil : manifest.ValidUntil, selected);
    }
    public bool IsRevoked(string serviceName, ServiceEndpoint endpoint) => discovery.IsRevoked(serviceName, endpoint);
}

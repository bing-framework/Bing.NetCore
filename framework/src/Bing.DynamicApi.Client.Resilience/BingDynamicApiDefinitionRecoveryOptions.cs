using Bing.Resilience;
using Bing.DynamicApi.Contracts;
using Microsoft.Extensions.DependencyInjection;
namespace Bing.DynamicApi.Client.Resilience;

public enum BingDynamicApiDefinitionRecoveryMode { SequentialRetry, Hedging }
public sealed record BingDefinitionEndpoint(string Id, Uri Address);
/// <summary>稳定授权范围和凭据受众。属性只保存不可变字符串快照，不持有请求 Scope。</summary>
public sealed record BingDefinitionAccess(string Scope, string CredentialAudience, IReadOnlyDictionary<string, string> Properties);
public sealed class BingDynamicApiDefinitionRecoveryOptions
{
    public bool Enabled { get; set; }
    public BingDynamicApiDefinitionRecoveryMode RecoveryMode { get; set; }
    public string ServiceName { get; set; } = "";
    public string ContractRevision { get; set; } = "";
    public string CompatibilityGroup { get; set; } = "";
    public string? ExpectedDigest { get; set; }
    public int MaxAttempts { get; set; } = 2;
    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(200);
    public int MaxWireBytes { get; set; } = 1024 * 1024;
    public int MaxDecodedBytes { get; set; } = 4 * 1024 * 1024;
    public int MaxJsonDepth { get; set; } = 64;
    public int MaxServices { get; set; } = 256;
    public int MaxMethods { get; set; } = 4096;
    public int MaxCachedDefinitions { get; set; } = 128;
    public TimeSpan CacheLifetime { get; set; } = TimeSpan.FromMinutes(10);
    public bool UseManifestCandidates { get; set; }
    public bool UseDiscoveryBootstrap { get; set; }
    public IList<BingDefinitionEndpoint> Endpoints { get; } = new List<BingDefinitionEndpoint>();
    public Func<Uri, bool>? TrustEndpoint { get; set; }
    public Func<BingDynamicApiDefinitionRequest, BingDefinitionAccess>? CaptureAccess { get; set; }
    public Func<IServiceProvider, BingDefinitionAccess, HttpRequestMessage, CancellationToken, ValueTask>? PrepareRequestAsync { get; set; }
    /// <summary>校验服务身份、契约版本及访问范围；老服务由宿主显式确认兼容定义组。</summary>
    public Func<HttpResponseMessage, BingDefinitionAccess, bool>? ValidateIdentity { get; set; }
    public Func<ReadOnlyMemory<byte>, bool>? VerifySignature { get; set; }
    public Func<BingDynamicApiDefinition, bool>? ValidateDefinition { get; set; }
    public Action<IHttpClientBuilder>? ConfigureTransport { get; set; }
    public Func<HttpMessageHandler, bool>? ValidateCustomTransport { get; set; }
    internal BingDynamicApiDefinitionRecoveryOptions Freeze()
    {
        if (!Enum.IsDefined(typeof(BingDynamicApiDefinitionRecoveryMode), RecoveryMode) ||
            string.IsNullOrWhiteSpace(ServiceName) || string.IsNullOrWhiteSpace(ContractRevision) && string.IsNullOrWhiteSpace(CompatibilityGroup) ||
            MaxAttempts is < 1 or > 16 || Delay < TimeSpan.Zero || Delay > TimeSpan.FromMinutes(1) ||
            MaxWireBytes is < 1 or > 16777216 || MaxDecodedBytes is < 1 or > 67108864 || MaxJsonDepth is < 1 or > 128 ||
            MaxServices is < 1 or > 4096 || MaxMethods is < 1 or > 65536 || MaxCachedDefinitions is < 1 or > 4096 ||
            CacheLifetime <= TimeSpan.Zero || CacheLifetime > TimeSpan.FromDays(1) || CaptureAccess is null || TrustEndpoint is null || ValidateIdentity is null ||
            !UseDiscoveryBootstrap && Endpoints.Count == 0 || Endpoints.Count > 128 ||
            Endpoints.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != Endpoints.Count ||
            ExpectedDigest is not null && (ExpectedDigest.Length != 64 || !ExpectedDigest.All(Uri.IsHexDigit)))
            throw new ResilienceConfigurationException("描述恢复配置、信任或身份校验不完整。");
        foreach (var endpoint in Endpoints)
            if (string.IsNullOrWhiteSpace(endpoint.Id) || endpoint.Id.Length > 128 || !ValidAddress(endpoint.Address) || TrustEndpoint(endpoint.Address) != true)
                throw new ResilienceConfigurationException("描述 bootstrap 地址不受信任。");
        var copy = (BingDynamicApiDefinitionRecoveryOptions)MemberwiseClone();
        copy._endpoints = Endpoints.ToArray();
        return copy;
    }
    private BingDefinitionEndpoint[]? _endpoints;
    internal IReadOnlyList<BingDefinitionEndpoint> FrozenEndpoints => _endpoints ?? Endpoints.ToArray();
    internal static bool ValidAddress(Uri uri) => uri is { IsAbsoluteUri: true } && uri.Scheme is "https" or "http" && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0;
}
internal sealed class DefinitionCandidateException(string message, Exception? inner = null) : Exception(message, inner);
internal sealed class DefinitionTrustException(string message) : Exception(message), IResilienceNonRecoverableFailure;

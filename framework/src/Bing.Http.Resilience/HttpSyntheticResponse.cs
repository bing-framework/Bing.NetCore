using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using Bing.Resilience;
using System.Security.Cryptography;
using System.Text;

namespace Bing.Http.Resilience;

public enum HttpSyntheticResponseKind { Cached, ContractDefault, DurableAccepted }
public sealed record HttpSyntheticRequest(ResilienceOperationDescriptor Operation, string Method, Uri Uri, string AccessScope,
    string? IdempotencyKey, string? BodyFingerprint);
public sealed record HttpSyntheticFailure(HttpStatusCode? StatusCode, Exception? Exception);
public sealed record HttpSyntheticResponse(HttpResponseMessage Response, HttpSyntheticResponseKind Kind, string OperationName,
    string ContractVersion, string AccessScope, string SourceId, DateTimeOffset? ValidUntil = null, IResilienceDurableCommitProof? CommitProof = null);
public interface IHttpSyntheticResponsePolicy
{
    ValueTask<HttpSyntheticResponse?> TryCreateAsync(HttpSyntheticRequest request, HttpSyntheticFailure failure, CancellationToken token);
}
public sealed record HttpResponseObservation(string ClientName, ResilienceOperationDescriptor? Operation,
    HttpStatusCode StatusCode, bool IsSynthetic, HttpSyntheticResponseKind? Kind, string? SourceId);
public interface IHttpResilienceResponseObserver
{
    ValueTask ObserveAsync(HttpResponseObservation response, CancellationToken token);
}
internal static class HttpSyntheticDiagnostics
{
    internal static readonly Meter Meter = new("Bing.Resilience");
    internal static readonly Counter<long> Counter = Meter.CreateCounter<long>("bing.resilience.http.synthetic");
    internal static void Record(string client, HttpSyntheticResponseKind kind) =>
        Counter.Add(1, new KeyValuePair<string, object?>("client", client), new KeyValuePair<string, object?>("kind", kind.ToString()));
}
internal static class HttpSyntheticResponseExecutor
{
    internal static async ValueTask<(HttpResponseMessage Response, HttpSyntheticResponse? Synthetic)> Execute(
        HttpRequestMessage request, string? fingerprint, BingHttpResilienceOptions options, TimeProvider clock,
        Func<ValueTask<HttpResponseMessage>> send, CancellationToken token)
    {
        HttpResponseMessage? original = null;
        Exception? error = null;
        try { original = await send().ConfigureAwait(false); }
        catch (Exception exception) when (exception is not OperationCanceledException && exception is not ResilienceTimeoutException { Kind: ResilienceTimeoutKind.Execution })
        { error = exception; }
        try
        {
            token.ThrowIfCancellationRequested();
            var canHandle = error is ResilienceCircuitOpenException ||
                HttpFailureClassifier.IsTransient(error is null ? ResilienceOutcome<HttpResponseMessage>.FromResult(original!) : ResilienceOutcome<HttpResponseMessage>.FromException(error), true);
            if (options.SyntheticResponsePolicy is not null && canHandle &&
                !Flag(request, BingHttpOperationMetadata.IsStreamResult) && !Flag(request, BingHttpOperationMetadata.IsDefinition) &&
                request.Options.TryGetValue(BingHttpOperationMetadata.Operation, out var operation) && options.SyntheticOperations.Contains(operation) &&
                request.Options.TryGetValue(BingHttpOperationMetadata.AccessScope, out var scope) && !string.IsNullOrWhiteSpace(scope))
            {
                request.Options.TryGetValue(BingHttpOperationMetadata.IdempotencyKey, out var key);
                var description = new HttpSyntheticRequest(operation, request.Method.Method, request.RequestUri!, scope, key, fingerprint);
                var replacement = await options.SyntheticResponsePolicy.TryCreateAsync(description, new(original?.StatusCode, error), token).ConfigureAwait(false);
                if (replacement is not null)
                {
                    try
                    {
                        if (ReferenceEquals(replacement.Response, original)) throw new ResilienceConfigurationException("合成响应必须是独立对象。");
                        await Validate(description, replacement, clock, token).ConfigureAwait(false);
                        token.ThrowIfCancellationRequested();
                    }
                    catch
                    {
                        if (!ReferenceEquals(replacement.Response, original)) replacement.Response?.Dispose();
                        throw;
                    }
                    original?.Dispose(); original = null;
                    return (replacement.Response, replacement);
                }
            }
            if (error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
            var result = original!; original = null;
            return (result, null);
        }
        finally { original?.Dispose(); }
    }
    private static bool Flag(HttpRequestMessage request, HttpRequestOptionsKey<bool> key) => request.Options.TryGetValue(key, out var value) && value;
    private static async ValueTask Validate(HttpSyntheticRequest request, HttpSyntheticResponse result, TimeProvider clock, CancellationToken token)
    {
        if (!Enum.IsDefined(typeof(HttpSyntheticResponseKind), result.Kind) || result.OperationName != request.Operation.Name ||
            result.ContractVersion != request.Operation.ContractVersion || result.AccessScope != request.AccessScope ||
            string.IsNullOrWhiteSpace(result.SourceId) || result.SourceId.Length > 128 ||
            result.Response is null || !result.Response.IsSuccessStatusCode || result.Response.Headers.Contains("Set-Cookie"))
            throw new ResilienceConfigurationException("合成响应的来源、身份范围或业务契约不一致。");
        if (result.Kind != HttpSyntheticResponseKind.DurableAccepted)
        {
            if (request.Operation.Kind != ResilienceOperationKind.ReadOnly || request.Method is not ("GET" or "HEAD") ||
                result.Response.StatusCode != HttpStatusCode.OK || result.ValidUntil <= clock.GetUtcNow() || result.ValidUntil is null)
                throw new ResilienceConfigurationException("缓存/默认响应仅允许有有效期的只读 200 契约。");
            return;
        }
        var proof = result.CommitProof;
        var expectedScope = Scope(request.Operation.Name, request.Operation.ContractVersion,
            Scope(request.Operation.IdempotencyScope!, request.Operation.ContractVersion, request.AccessScope));
        if (request.Operation.Kind != ResilienceOperationKind.IdempotentWrite || result.Response.StatusCode != HttpStatusCode.Accepted ||
            proof is null || string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.BodyFingerprint is null ||
            proof.KeyHash != Hash(request.IdempotencyKey) || proof.ScopeHash != expectedScope || proof.Fingerprint != request.BodyFingerprint ||
            proof.Deadline <= clock.GetUtcNow() || result.Response.Headers.Location is null || !await proof.VerifyAsync(token).ConfigureAwait(false))
            throw new ResilienceConfigurationException("202 需要同键、同载荷且已持久提交的受理凭证与查询地址。");
        result.Response.Headers.Remove("X-Bing-Operation-Id");
        result.Response.Headers.TryAddWithoutValidation("X-Bing-Operation-Id", proof.OperationId);
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Scope(string operation, string version, string access) => Hash(string.Join(":", operation.Length, operation, version.Length, version, access.Length, access));
}

using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Bing.Resilience.Operations;

namespace Bing.Resilience.Idempotency.AspNetCore;

/// <summary>显式参与同事务的端点契约；业务必须从 Feature 复用事务，不得在此回调内执行未经幂等保护的外部副作用。</summary>
public sealed record IdempotentEndpoint(ResilienceOperationDescriptor Operation, string AccessScope);
public sealed class HttpIdempotencyOptions
{
    public Func<HttpContext, IdempotentEndpoint?>? ResolveEndpoint { get; set; }
    public string KeyHeader { get; set; } = "Idempotency-Key";
    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(1);
    public int MaxRequestBytes { get; set; } = 1024 * 1024;
    public int MaxResponseBytes { get; set; } = 1024 * 1024;
    public ISet<string> StoredResponseHeaders { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Content-Type", "Location", "ETag" };
    public ISet<string> SemanticRequestHeaders { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Content-Type", "If-Match" };
}
public interface IOperationTransactionFeature { IOperationTransaction Transaction { get; } }
public static class HttpIdempotencyExtensions
{
    /// <summary>放在认证、授权及路由之后。只有 ResolveEndpoint 明确返回的端点参与事务。</summary>
    public static IApplicationBuilder UseBingIdempotency(this IApplicationBuilder app, Action<HttpIdempotencyOptions> configure)
    {
        var options = new HttpIdempotencyOptions(); configure(options);
        if (options.ResolveEndpoint is null || options.MaxRequestBytes <= 0 || options.MaxResponseBytes <= 0 ||
            options.Retention <= TimeSpan.Zero || options.Retention > TimeSpan.FromDays(30) ||
            string.IsNullOrWhiteSpace(options.KeyHeader) || options.StoredResponseHeaders.Any(x =>
                x.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase) || x.Equals("Authorization", StringComparison.OrdinalIgnoreCase)))
            throw new ResilienceConfigurationException("接收端幂等配置无效。");
        var frozen = new HttpIdempotencyOptions { ResolveEndpoint = options.ResolveEndpoint, KeyHeader = options.KeyHeader,
            MaxRequestBytes = options.MaxRequestBytes, MaxResponseBytes = options.MaxResponseBytes, Retention = options.Retention };
        frozen.StoredResponseHeaders.Clear(); foreach (var h in options.StoredResponseHeaders) frozen.StoredResponseHeaders.Add(h);
        frozen.SemanticRequestHeaders.Clear(); foreach (var h in options.SemanticRequestHeaders) frozen.SemanticRequestHeaders.Add(h);
        return app.Use(next => context => InvokeAsync(context, next, frozen));
    }
    private sealed record TransactionFeature(IOperationTransaction Transaction) : IOperationTransactionFeature;
    private sealed record StoredResponse(int Status, Dictionary<string, string[]> Headers, byte[] Body);
    private sealed class UnsuccessfulResponse(StoredResponse response) : Exception { public StoredResponse Response => response; }

    private static async Task InvokeAsync(HttpContext context, RequestDelegate next, HttpIdempotencyOptions options)
    {
        var endpoint = options.ResolveEndpoint!(context);
        if (endpoint is null) { await next(context).ConfigureAwait(false); return; }
        var catalog = context.RequestServices.GetRequiredService<ResilienceOperationCatalog>();
        if (catalog.Get(endpoint.Operation.Name) != endpoint.Operation || endpoint.Operation.Kind != ResilienceOperationKind.IdempotentWrite ||
            string.IsNullOrWhiteSpace(endpoint.AccessScope))
            throw new ResilienceConfigurationException("端点未登记同事务幂等写入许可。");
        if (context.Request.Headers[options.KeyHeader].Count != 1 || string.IsNullOrWhiteSpace(context.Request.Headers[options.KeyHeader][0]))
        { context.Response.StatusCode = StatusCodes.Status400BadRequest; return; }
        var key = context.Request.Headers[options.KeyHeader][0]!;
        var token = context.RequestAborted;
        using var requestBody = new BoundedBuffer(options.MaxRequestBytes);
        try { await context.Request.Body.CopyToAsync(requestBody, token).ConfigureAwait(false); }
        catch (PayloadTooLargeException) { context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge; return; }
        var originalRequestBody = context.Request.Body;
        var originalFeature = context.Features.Get<IHttpResponseBodyFeature>()!;
        var previousTransaction = context.Features.Get<IOperationTransactionFeature>();
        var clock = context.RequestServices.GetRequiredService<TimeProvider>();
        var fingerprintParts = new List<string> { context.Request.Method, context.Request.PathBase + context.Request.Path + context.Request.QueryString,
            OperationFingerprint.Hash(requestBody.ToArray()) };
        foreach (var header in options.SemanticRequestHeaders.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            fingerprintParts.Add(header.ToLowerInvariant());
            foreach (var value in context.Request.Headers[header]) fingerprintParts.Add(value ?? "");
        }
        var fingerprint = OperationFingerprint.Hash(string.Join("", fingerprintParts.Select(x => x.Length + ":" + x)));
        StoredResponse? output;
        try
        {
            var coordinator = context.RequestServices.GetRequiredService<IdempotencyCoordinator>();
            var result = await coordinator.ExecuteAsync(endpoint.Operation, endpoint.AccessScope, key, fingerprint, clock.GetUtcNow() + options.Retention,
                async (transaction, ct) =>
                {
                    requestBody.Position = 0; context.Request.Body = requestBody;
                    using var responseBody = new BoundedBuffer(options.MaxResponseBytes);
                    var buffering = new StreamResponseBodyFeature(responseBody);
                    context.Features.Set<IHttpResponseBodyFeature>(buffering);
                    context.Features.Set<IOperationTransactionFeature>(new TransactionFeature(transaction));
                    await next(context).ConfigureAwait(false);
                    await buffering.CompleteAsync().ConfigureAwait(false);
                    ct.ThrowIfCancellationRequested();
                    var response = new StoredResponse(context.Response.StatusCode,
                        context.Response.Headers.Where(h => options.StoredResponseHeaders.Contains(h.Key)).ToDictionary(h => h.Key, h => h.Value.Select(x => x ?? "").ToArray(), StringComparer.OrdinalIgnoreCase),
                        responseBody.ToArray());
                    // 错误响应不提交业务事务，不把部分业务写入留在数据库中。
                    if (response.Status is < 200 or >= 300) throw new UnsuccessfulResponse(response);
                    return response;
                }, token).ConfigureAwait(false);
            if (result.Kind != IdempotencyResultKind.Completed)
            {
                output = new StoredResponse(result.Kind == IdempotencyResultKind.Expired ? StatusCodes.Status410Gone : StatusCodes.Status409Conflict,
                    new() { ["Retry-After"] = new[] { "1" } }, Array.Empty<byte>());
            }
            else output = result.Result!;
        }
        catch (OperationIdempotencyConflictException) { output = new(StatusCodes.Status409Conflict, new(), Array.Empty<byte>()); }
        catch (UnsuccessfulResponse error) { output = error.Response; }
        finally
        {
            context.Request.Body = originalRequestBody;
            context.Features.Set(originalFeature);
            context.Features.Set(previousTransaction);
        }
        // 只有 Commit 返回成功之后才向原始传输写入业务成功响应。
        context.Response.Headers.Clear();
        context.Response.StatusCode = output.Status;
        foreach (var h in output.Headers) context.Response.Headers[h.Key] = h.Value;
        context.Response.ContentLength = output.Body.Length;
        await context.Response.Body.WriteAsync(output.Body.AsMemory(), token).ConfigureAwait(false);
    }
    private sealed class PayloadTooLargeException : Exception;
    private sealed class BoundedBuffer(int maximum) : MemoryStream
    {
        private void Check(int count) { if (Position + count > maximum) throw new PayloadTooLargeException(); }
        public override void Write(byte[] buffer, int offset, int count) { Check(count); base.Write(buffer, offset, count); }
        public override void Write(ReadOnlySpan<byte> buffer) { Check(buffer.Length); base.Write(buffer); }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        { Check(buffer.Length); return base.WriteAsync(buffer, cancellationToken); }
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        { Check(count); return base.WriteAsync(buffer, offset, count, cancellationToken); }
        public override void WriteByte(byte value) { Check(1); base.WriteByte(value); }
        public override void SetLength(long value) { if (value > maximum) throw new PayloadTooLargeException(); base.SetLength(value); }
    }
}

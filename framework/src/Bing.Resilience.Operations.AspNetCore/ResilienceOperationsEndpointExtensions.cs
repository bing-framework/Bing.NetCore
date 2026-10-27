using System.Security.Claims;
using Bing.Resilience.Compensation;
using Bing.Resilience.Operations;
using Bing.Resilience.Replay;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Resilience.Operations.AspNetCore;

/// <summary>持久操作管理接入。宿主必须提供身份认证、权限和访问范围。</summary>
public sealed class ResilienceOperationsEndpointOptions
{
    public string ReplayPrefix { get; set; } = "/_resilience/replay";
    public string SagaPrefix { get; set; } = "/_resilience/saga";
    public string ConsolePath { get; set; } = "/_resilience";
    public bool EnableConsole { get; set; }
    public Func<HttpContext, ValueTask<string?>>? ResolveAccessScope { get; set; }
    public Func<HttpContext, string, ValueTask<bool>>? AuthorizeAsync { get; set; }
    public Func<HttpContext, string?> ResolveActor { get; set; } = context =>
        context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? context.User.Identity?.Name;
}

/// <summary>安全状态投影，不包含载荷、幂等键、租约令牌或加密文档。</summary>
public sealed record ResilienceOperationStatus(string Id, string Kind, string State, long Version, int Attempts,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset NextAttemptAt, DateTimeOffset Deadline, bool LeaseActive);
public sealed record ResilienceSagaStatus(ResilienceOperationStatus Saga, IReadOnlyList<ResilienceOperationStatus> Steps);
public sealed record CancelReplayRequest(long ExpectedVersion, string Reason);
public sealed record SagaManualRequest(long ExpectedSagaVersion, SagaManualAction Action, string Reason);

/// <summary>可选管理端点和控制台。所有状态变更均使用协调器的授权范围、版本 CAS 和审计。</summary>
public static class ResilienceOperationsEndpointExtensions
{
    /// <summary>注册管理端点使用的防伪服务；不会提供认证方案或默认权限。</summary>
    public static IServiceCollection AddBingResilienceOperationsManagement(this IServiceCollection services)
    {
        services.AddAntiforgery(options => options.HeaderName = "X-Bing-Resilience-CSRF");
        return services;
    }

    public static IEndpointRouteBuilder MapBingResilienceOperations(this IEndpointRouteBuilder endpoints,
        Action<ResilienceOperationsEndpointOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var mutable = new ResilienceOperationsEndpointOptions(); configure(mutable); Validate(mutable);
        // 复制委托和标量，注册之后修改原 options 不得改变授权或路由。
        var options = new ResilienceOperationsEndpointOptions
        {
            ReplayPrefix = mutable.ReplayPrefix, SagaPrefix = mutable.SagaPrefix, ConsolePath = mutable.ConsolePath,
            EnableConsole = mutable.EnableConsole, ResolveAccessScope = mutable.ResolveAccessScope,
            ResolveActor = mutable.ResolveActor, AuthorizeAsync = mutable.AuthorizeAsync
        };
        var antiforgery = endpoints.ServiceProvider.GetRequiredService<IAntiforgery>();
        var registrations = endpoints.ServiceProvider.GetRequiredService<IServiceProviderIsService>();
        if (!registrations.IsService(typeof(IReplayCoordinator)) || !registrations.IsService(typeof(ISagaCoordinator)))
            throw new ResilienceConfigurationException("管理接入需要先注册 Durable Replay 与 Saga 协调器。");
        var clock = endpoints.ServiceProvider.GetService<TimeProvider>() ?? TimeProvider.System;
        endpoints.MapGet(options.ConsolePath + "/session", async (HttpContext context) =>
        {
            var access = await AccessAsync(context, "operations.session", options);
            if (access.Error is { } error) return error;
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new { token = tokens.RequestToken, headerName = tokens.HeaderName });
        });
        if (options.EnableConsole)
            endpoints.MapGet(options.ConsolePath, async (HttpContext context) =>
            {
                var access = await AccessAsync(context, "operations.console", options);
                return access.Error ?? OperationsConsole.Render(context, options);
            });
        endpoints.MapGet(options.ReplayPrefix + "/{operationId}", async (HttpContext context, string operationId, [FromServices] IReplayCoordinator coordinator) =>
        {
            var access = await AccessAsync(context, "replay.read", options);
            if (access.Error is { } error) return error;
            try
            {
                var record = await coordinator.GetAsync(operationId, access.Scope!, context.RequestAborted);
                return record is null ? Results.NotFound() : Results.Ok(ToStatus(record, clock));
            }
            catch (UnauthorizedAccessException) { return Results.NotFound(); }
        });
        endpoints.MapPost(options.ReplayPrefix + "/{operationId}/cancel", async (HttpContext context, string operationId,
            CancelReplayRequest request, [FromServices] IReplayCoordinator coordinator) =>
        {
            var access = await AccessAsync(context, "replay.cancel", options);
            if (access.Error is { } error) return error;
            if (!await ValidateMutationAsync(context, antiforgery) || request is null || request.ExpectedVersion < 0 || !ValidReason(request.Reason)) return Results.BadRequest();
            try
            {
                await coordinator.CancelAsync(operationId, access.Scope!, request.ExpectedVersion, access.Actor!, request.Reason, context.RequestAborted);
                return Results.NoContent();
            }
            catch (UnauthorizedAccessException) { return Results.NotFound(); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
            catch (OperationConcurrencyException) { return Results.Conflict(); }
            catch (ResilienceConfigurationException) { return InvalidState(); }
        });
        endpoints.MapGet(options.SagaPrefix + "/{sagaId}", async (HttpContext context, string sagaId, [FromServices] ISagaCoordinator coordinator) =>
        {
            var access = await AccessAsync(context, "saga.read", options);
            if (access.Error is { } error) return error;
            try
            {
                var snapshot = await coordinator.GetAsync(sagaId, access.Scope!, context.RequestAborted);
                return snapshot is null ? Results.NotFound() : Results.Ok(new ResilienceSagaStatus(ToStatus(snapshot.Saga, clock), snapshot.Steps.Select(s => ToStatus(s, clock)).ToArray()));
            }
            catch (UnauthorizedAccessException) { return Results.NotFound(); }
        });
        endpoints.MapPost(options.SagaPrefix + "/{sagaId}/steps/{stepId}/manual", async (HttpContext context, string sagaId, string stepId,
            SagaManualRequest request, [FromServices] ISagaCoordinator coordinator) =>
        {
            var access = await AccessAsync(context, "saga.manual", options);
            if (access.Error is { } error) return error;
            if (!await ValidateMutationAsync(context, antiforgery) || request is null || request.ExpectedSagaVersion < 0 ||
                !ValidReason(request.Reason) || !Enum.IsDefined(typeof(SagaManualAction), request.Action)) return Results.BadRequest();
            try
            {
                await coordinator.ApplyManualAsync(sagaId, stepId, access.Scope!, request.ExpectedSagaVersion, request.Action, access.Actor!, request.Reason, context.RequestAborted);
                return Results.NoContent();
            }
            catch (UnauthorizedAccessException) { return Results.NotFound(); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
            catch (OperationConcurrencyException) { return Results.Conflict(); }
            catch (ResilienceConfigurationException) { return InvalidState(); }
        });
        return endpoints;
    }

    private static IResult InvalidState() => Results.Problem("该状态不允许所请求的人工操作。", statusCode: StatusCodes.Status422UnprocessableEntity);
    private static bool ValidReason(string? reason) => !string.IsNullOrWhiteSpace(reason) && reason.Length <= 512;
    private static async Task<bool> ValidateMutationAsync(HttpContext context, IAntiforgery antiforgery)
    {
        try { await antiforgery.ValidateRequestAsync(context); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }
    private sealed record Access(string? Scope = null, string? Actor = null, IResult? Error = null);
    private static async ValueTask<Access> AccessAsync(HttpContext context, string permission, ResilienceOperationsEndpointOptions options)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        if (context.User.Identity?.IsAuthenticated != true) return new(Error: Results.Unauthorized());
        if (!await options.AuthorizeAsync!(context, permission)) return new(Error: Results.StatusCode(StatusCodes.Status403Forbidden));
        var scope = await options.ResolveAccessScope!(context); var actor = options.ResolveActor(context);
        if (string.IsNullOrWhiteSpace(scope) || string.IsNullOrWhiteSpace(actor) || actor.Length > 128)
            return new(Error: Results.StatusCode(StatusCodes.Status403Forbidden));
        return new(scope, actor);
    }
    private static ResilienceOperationStatus ToStatus(OperationRecord record, TimeProvider clock) => new(record.Id, record.Kind, record.State,
        record.Version, record.Attempts, record.CreatedAt, record.UpdatedAt, record.NextAttemptAt, record.Deadline,
        record.LeaseUntil is { } until && until > clock.GetUtcNow());
    private static void Validate(ResilienceOperationsEndpointOptions options)
    {
        foreach (var path in new[] { options.ReplayPrefix, options.SagaPrefix, options.ConsolePath })
            if (string.IsNullOrWhiteSpace(path) || path.Length > 256 || !path.StartsWith('/') || path.EndsWith('/') || path.Contains("//", StringComparison.Ordinal) ||
                path.Any(c => !(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' || c is '/' or '-' or '_')))
                throw new ResilienceConfigurationException("管理路径必须由 ASCII 字母、数字、斜杠、下划线或连字符组成，不能以斜杠结尾。");
        if (new[] { options.ReplayPrefix, options.SagaPrefix, options.ConsolePath }.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 3 ||
            options.AuthorizeAsync is null || options.ResolveAccessScope is null || options.ResolveActor is null)
            throw new ResilienceConfigurationException("管理端点需要独立路径及宿主权限、身份和访问范围解析器。");
        // Console 与 session 是静态路由，不能截获 Replay/Saga 的单段 ID 查询。
        foreach (var prefix in new[] { options.ReplayPrefix, options.SagaPrefix })
        foreach (var path in new[] { options.ConsolePath, options.ConsolePath + "/session" })
            if (path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase) && !path[(prefix.Length + 1)..].Contains('/'))
                throw new ResilienceConfigurationException("控制台路径与操作 ID 查询路由冲突。");
    }
}

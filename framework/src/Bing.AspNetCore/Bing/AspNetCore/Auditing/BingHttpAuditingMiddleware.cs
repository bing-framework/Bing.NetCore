using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Security.Claims;
using System.Text;
using Bing.Auditing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bing.AspNetCore.Auditing;

/// <summary>
/// 采集 HTTP 请求元数据并保存为审计日志。
/// </summary>
public sealed class BingHttpAuditingMiddleware
{
    /// <summary>
    /// 审计清理异常写入请求异常的数据键。
    /// </summary>
    private const string CleanupErrorsKey = "Bing.HttpAuditCleanupErrors";

    /// <summary>
    /// 日志中最多输出的异常类型数量。
    /// </summary>
    private const int MaxLoggedErrorTypes = 4;

    /// <summary>
    /// 后续请求处理器。
    /// </summary>
    private readonly RequestDelegate _next;

    /// <summary>
    /// 审计配置。
    /// </summary>
    private readonly BingHttpAuditingOptions _options;

    /// <summary>
    /// 日志记录器。
    /// </summary>
    private readonly ILogger<BingHttpAuditingMiddleware> _logger;

    /// <summary>
    /// 构造时确定的查询字段白名单。
    /// </summary>
    private readonly HashSet<string> _allowedQueryParameters;

    /// <summary>
    /// 初始化一个 <see cref="BingHttpAuditingMiddleware" /> 类型的实例。
    /// </summary>
    /// <param name="next">后续请求处理器。</param>
    /// <param name="options">HTTP 审计配置。</param>
    /// <param name="logger">可选的审计故障日志记录器。</param>
    public BingHttpAuditingMiddleware(RequestDelegate next,
        IOptions<BingHttpAuditingOptions> options, ILogger<BingHttpAuditingMiddleware> logger = null)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        if (_options.MaxUrlLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "审计 URL 长度必须大于零。");
        if (_options.SaveTimeout < Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(options), "审计保存等待预算不能小于无限等待值。");
        _logger = logger;
        _allowedQueryParameters = new HashSet<string>(_options.AllowedQueryParameters, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 处理请求并保存审计信息。
    /// </summary>
    /// <param name="context">当前请求上下文。</param>
    /// <param name="manager">当前请求作用域的审计管理器。</param>
    public async Task InvokeAsync(HttpContext context, IAuditingManager manager)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        if (manager == null)
            throw new ArgumentNullException(nameof(manager));
        if (!_options.Enabled || HasDisableAuditingMarker(context))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var handle = manager.BeginAsyncScope();
        AuditLogInfo log = null;
        var stopwatch = Stopwatch.StartNew();
        Exception requestException = null;
        try
        {
            log = manager.Current?.Log ?? throw new InvalidOperationException("审计管理器未创建当前作用域。");
            PopulateRequest(log, context);
            await _next(context).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            requestException = exception;
            log?.Exceptions.Add(exception);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            if (log != null)
            {
                log.HttpStatusCode = context.Response.StatusCode;
                log.ExecutionDuration = stopwatch.ElapsedMilliseconds > int.MaxValue
                    ? int.MaxValue
                    : (int)stopwatch.ElapsedMilliseconds;
            }
            var cleanupErrors = new List<Exception>();
            try
            {
                if (log != null)
                    await SaveWithBudgetAsync(handle, context.RequestAborted, cleanupErrors).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                AddCleanupError(cleanupErrors, exception);
            }
            try
            {
                await handle.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                AddCleanupError(cleanupErrors, exception);
            }
            if (cleanupErrors.Count > 0)
            {
                var cleanupErrorArray = cleanupErrors.ToArray();
                var primaryException = requestException ?? cleanupErrorArray[0];
                AttachCleanupErrors(primaryException, cleanupErrorArray);
                var errorTypes = string.Join(",", cleanupErrorArray
                    .Select(exception => exception.GetType().Name)
                    .Distinct(StringComparer.Ordinal)
                    .Take(MaxLoggedErrorTypes));
                // 数据库异常和 URL 可能包含秘密，只输出有限的关联信息和异常类型。
                _logger?.LogError(
                    "HTTP 审计日志保存或释放失败。ErrorCount={ErrorCount}, ErrorType={ErrorType}, CorrelationId={CorrelationId}",
                    cleanupErrorArray.Length, errorTypes, Trim(context.TraceIdentifier, 128));
                if (requestException == null && _options.ThrowOnStoreFailure)
                    ExceptionDispatchInfo.Capture(primaryException).Throw();
            }
        }
    }

    /// <summary>
    /// 按配置等待审计保存，并在超时后继续等待实际任务完成。
    /// </summary>
    /// <param name="handle">当前审计保存句柄。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <param name="cleanupErrors">保存诊断及异常集合。</param>
    /// <remarks>超时只表示调用方等待预算耗尽，不会取消或提前释放底层存储。</remarks>
    private async Task SaveWithBudgetAsync(IAuditLogSaveHandle handle, CancellationToken cancellationToken,
        ICollection<Exception> cleanupErrors)
    {
        var saveTask = handle is ICancellableAuditLogSaveHandle cancellable
            ? cancellable.SaveAsync(cancellationToken)
            : handle.SaveAsync();
        if (saveTask == null)
            throw new InvalidOperationException("审计保存句柄返回了空任务。");
        if (_options.SaveTimeout != Timeout.InfiniteTimeSpan && !saveTask.IsCompleted)
        {
            using var timeoutCancellation = new CancellationTokenSource();
            var timeoutTask = Task.Delay(_options.SaveTimeout, timeoutCancellation.Token);
            try
            {
                var completed = await Task.WhenAny(saveTask, timeoutTask).ConfigureAwait(false);
                if (!ReferenceEquals(completed, saveTask))
                    AddCleanupError(cleanupErrors, new TimeoutException("HTTP 审计保存超过等待预算，仍将等待实际任务完成。"));
            }
            finally
            {
                // 保存提前完成时取消定时器，避免长预算在请求结束后继续保留任务和计时器。
                timeoutCancellation.Cancel();
            }
        }
        await saveTask.ConfigureAwait(false);
    }

    /// <summary>
    /// 按对象引用去重并收集清理异常。
    /// </summary>
    /// <param name="errors">当前请求已经收集的清理异常。</param>
    /// <param name="exception">待收集的清理异常。</param>
    private static void AddCleanupError(ICollection<Exception> errors, Exception exception)
    {
        if (!errors.Any(error => ReferenceEquals(error, exception)))
            errors.Add(exception);
    }

    /// <summary>
    /// 将完整清理异常数组附加到最终保留的异常对象。
    /// </summary>
    /// <param name="target">最终保留的异常对象。</param>
    /// <param name="cleanupErrors">按清理执行顺序排列的异常数组。</param>
    private static void AttachCleanupErrors(Exception target, IReadOnlyList<Exception> cleanupErrors)
    {
        var errors = new List<Exception>();
        if (target.Data[CleanupErrorsKey] is IEnumerable<Exception> existing)
        {
            foreach (var exception in existing)
                AddCleanupError(errors, exception);
        }
        foreach (var exception in cleanupErrors)
            AddCleanupError(errors, exception);
        target.Data[CleanupErrorsKey] = errors.ToArray();
    }

    /// <summary>
    /// 填写不包含请求正文的 HTTP 审计元数据。
    /// </summary>
    /// <param name="log">当前请求的审计日志。</param>
    /// <param name="context">当前请求上下文。</param>
    private void PopulateRequest(AuditLogInfo log, HttpContext context)
    {
        log.ApplicationName = _options.ApplicationName;
        log.HttpMethod = context.Request.Method;
        log.Url = CreateUrl(context);
        log.ClientIpAddress = context.Connection.RemoteIpAddress?.ToString();
        log.CorrelationId = context.TraceIdentifier;
        var identity = GetFirstAuthenticatedIdentity(context.User);
        log.UserId = FindIdentityClaim(identity, _options.UserIdClaimType);
        log.UserName = FindIdentityClaim(identity, _options.UserNameClaimType);
        log.TenantId = FindIdentityClaim(identity, _options.TenantClaimType);
        log.ClientId = FindIdentityClaim(identity, _options.ClientIdClaimType);
    }

    /// <summary>
    /// 生成仅包含允许字段的请求 URL。
    /// </summary>
    /// <param name="context">当前请求上下文。</param>
    /// <returns>受长度限制的路径与可选查询字段。</returns>
    private string CreateUrl(HttpContext context)
    {
        var result = new StringBuilder(Trim(context.Request.PathBase + context.Request.Path, _options.MaxUrlLength));
        var separator = '?';
        foreach (var name in _allowedQueryParameters.OrderBy(item => item, StringComparer.Ordinal))
        {
            if (result.Length >= _options.MaxUrlLength)
                break;
            if (!context.Request.Query.TryGetValue(name, out var values))
                continue;
            foreach (var value in values)
            {
                if (result.Length >= _options.MaxUrlLength)
                    break;
                result.Append(separator).Append(Uri.EscapeDataString(name)).Append('=')
                    .Append(Uri.EscapeDataString(Trim(value, _options.MaxUrlLength) ?? string.Empty));
                separator = '&';
            }
        }
        return Trim(result.ToString(), _options.MaxUrlLength);
    }

    /// <summary>
    /// 按声明主体顺序获取第一个已认证身份。
    /// </summary>
    /// <param name="principal">当前请求的声明主体。</param>
    /// <returns>第一个已认证身份；不存在时返回 null。</returns>
    private static ClaimsIdentity GetFirstAuthenticatedIdentity(ClaimsPrincipal principal)
    {
        foreach (var identity in principal.Identities)
        {
            if (identity.IsAuthenticated)
                return identity;
        }

        return null;
    }

    /// <summary>
    /// 从选定身份读取配置的声明。
    /// </summary>
    /// <param name="identity">当前请求选定的已认证身份；不存在时为空。</param>
    /// <param name="type">需要读取的声明类型。</param>
    /// <returns>声明值；身份、声明类型或声明不存在时返回 null。</returns>
    private static string FindIdentityClaim(ClaimsIdentity identity, string type) =>
        identity == null || string.IsNullOrWhiteSpace(type)
            ? null
            : identity.Claims.FirstOrDefault(claim => claim.Type == type)?.Value;

    /// <summary>
    /// 按配置长度截断文本。
    /// </summary>
    /// <param name="value">待截断的文本。</param>
    /// <param name="maxLength">允许的最大字符数。</param>
    /// <returns>不拆分 UTF-16 代理字符的受限文本；输入为空时返回 null。</returns>
    private static string Trim(string value, int maxLength)
    {
        if (value == null || value.Length <= maxLength)
            return value;
        if (char.IsHighSurrogate(value[maxLength - 1]))
            maxLength--;
        return value[..maxLength];
    }

    /// <summary>
    /// 识别中立审计契约及旧实现中的禁用标记。
    /// </summary>
    /// <param name="context">当前请求上下文。</param>
    /// <returns>端点包含禁用审计标记时返回 true，否则返回 false。</returns>
    private static bool HasDisableAuditingMarker(HttpContext context) =>
        context.GetEndpoint()?.Metadata.Any(metadata =>
            string.Equals(metadata?.GetType().FullName, "Bing.Auditing.DisableAuditingAttribute",
                StringComparison.Ordinal)) == true;
}

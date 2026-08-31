using Bing.Locks;
using Bing.Properties;
using Bing.Users;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.AspNetCore.Mvc.Filters;

/// <summary>
/// 通过锁定业务请求标识防止短时间内重复提交的 MVC 过滤器。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class AntiDuplicateRequestAttribute : ActionFilterAttribute
{
    /// <summary>
    /// 获取或设置用于区分业务请求的锁标识。
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// 获取或设置是否使用分布式锁实现请求互斥。
    /// </summary>
    public bool IsDistributed { get; set; }

    /// <summary>
    /// 获取或设置锁标识的生成类型，默认按用户区分。
    /// </summary>
    public LockType Type { get; set; } = LockType.User;

    /// <summary>
    /// 获取或设置允许再次提交前的锁定时间间隔，单位为秒。
    /// </summary>
    public int Interval { get; set; } = 30;

    /// <summary>
    /// 获取或设置检测到重复提交时返回的提示消息。
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// 获取或设置请求处理完成后是否自动释放锁；默认值为 <see langword="false"/>。
    /// </summary>
    public bool AutoUnLock { get; set; } = false;

    /// <summary>
    /// 获取锁后执行 MVC 操作，并根据配置在操作完成后释放锁。
    /// </summary>
    /// <param name="context">操作执行上下文</param>
    /// <param name="next">操作执行下一步委托</param>
    /// <exception cref="ArgumentNullException">执行上下文或后续执行委托为空时抛出。</exception>
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        if (next == null)
            throw new ArgumentNullException(nameof(next));

        var @lock = CreateLock(context);
        var key = GetKey(context);
        var value = GetValue(context);
        var isSuccess = false;
        try
        {
            isSuccess = await @lock.LockTakeAsync(key, value, GetExpiration());
            if (isSuccess == false)
            {
                context.Result = new ApiResult(StatusCode.Fail, GetFailMessage());
                return;
            }
            OnActionExecuting(context);
            if (context.Result != null)
                return;
            var executedContext = await next();
            OnActionExecuted(executedContext);
        }
        finally
        {
            // 并发模式下，需要释放锁
            if (isSuccess && AutoUnLock)
                await @lock.LockReleaseAsync(key, value);
        }
    }

    /// <summary>
    /// 创建锁
    /// </summary>
    /// <param name="context">操作执行上下文</param>
    /// <returns>根据当前请求配置解析出的锁实例。</returns>
    private ILock CreateLock(ActionExecutingContext context) => IsDistributed
        ? context.HttpContext.RequestServices.GetService<IDistributedLock>()
        : context.HttpContext.RequestServices.GetService<ILock>();

    /// <summary>
    /// 获取锁定标识
    /// </summary>
    /// <param name="context">操作执行上下文</param>
    /// <returns>当前请求使用的锁定标识。</returns>
    protected virtual string GetKey(ActionExecutingContext context)
    {
        var currentUser = context.HttpContext.RequestServices.GetService<ICurrentUser>();
        var userId = string.Empty;
        if (Type == LockType.User)
            userId = $"{currentUser.UserId}_";
        return string.IsNullOrWhiteSpace(Key) ? $"ADR:{userId}{context.HttpContext.Request.Path}" : $"ADR:{userId}{Key}";
    }

    /// <summary>
    /// 获取当前占用值
    /// </summary>
    /// <param name="context">操作执行上下文</param>
    /// <returns>当前请求使用的锁占用值。</returns>
    protected virtual string GetValue(ActionExecutingContext context)
    {
        var currentUser = context.HttpContext.RequestServices.GetService<ICurrentUser>();
        var value = string.Empty;
        if (Type == LockType.User && currentUser.IsAuthenticated)
            value = $"{currentUser.GetUserId()}";
        return string.IsNullOrWhiteSpace(value) ? "bing_global_lock" : value;
    }

    /// <summary>
    /// 获取到期时间间隔
    /// </summary>
    /// <returns>配置的锁到期时间间隔。</returns>
    private TimeSpan GetExpiration() => Interval == 0 ? TimeSpan.FromSeconds(30) : TimeSpan.FromSeconds(Interval);

    /// <summary>
    /// 获取失败消息
    /// </summary>
    /// <returns>配置的失败消息；未配置时返回与锁类型对应的默认消息。</returns>
    protected virtual string GetFailMessage() => !string.IsNullOrWhiteSpace(Message) ? Message : Type == LockType.User ? R.UserDuplicateRequest : R.GlobalDuplicateRequest;
}

/// <summary>
/// 锁类型
/// </summary>
public enum LockType
{
    /// <summary>
    /// 用户锁，当用户发出多个执行该操作的请求，只有第一个请求被执行，其它请求被抛弃，其它用户不受影响
    /// </summary>
    User = 0,

    /// <summary>
    /// 全局锁，该操作同时只有一个用户请求被执行
    /// </summary>
    Global = 1
}
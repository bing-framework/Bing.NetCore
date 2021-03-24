using System;
using System.Threading.Tasks;
using Bing.Locks;
using Bing.Properties;
using Bing.Users;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.AspNetCore.Mvc.Filters
{
    /// <summary>
    /// 防止重复提交过滤器
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class AntiDuplicateRequestAttribute : ActionFilterAttribute
    {
        /// <summary>
        /// 业务标识
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// 是否分布式
        /// </summary>
        public bool IsDistributed { get; set; }

        /// <summary>
        /// 锁类型
        /// </summary>
        public LockType Type { get; set; } = LockType.User;

        /// <summary>
        /// 再次提交时间间隔，单位：秒
        /// </summary>
        public int Interval { get; set; } = 30;

        /// <summary>
        /// 执行
        /// </summary>
        /// <param name="context">操作执行上下文</param>
        /// <param name="next">操作执行下一步委托</param>
        /// <exception cref="ArgumentNullException"></exception>
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (next == null)
                throw new ArgumentNullException(nameof(next));

            var @lock = CreateLock(context);
            var key = GetKey(context);
            var value = GetValue(context);
            if (await @lock.LockTakeAsync(key, value, GetExpiration()))
            {
                try
                {
                    await next();
                }
                finally
                {
                    await @lock.LockReleaseAsync(key, value);
                }
            }
            else
            {
                context.Result = new ApiResult(StatusCode.Fail, GetFailMessage());
            }
        }

        /// <summary>
        /// 创建锁
        /// </summary>
        /// <param name="context">操作执行上下文</param>
        private ILock CreateLock(ActionExecutingContext context) => IsDistributed
            ? context.HttpContext.RequestServices.GetService<IDistributedLock>()
            : context.HttpContext.RequestServices.GetService<ILock>();

        /// <summary>
        /// 获取锁定标识
        /// </summary>
        /// <param name="context">操作执行上下文</param>
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
        private TimeSpan GetExpiration() => Interval == 0 ? TimeSpan.FromSeconds(30) : TimeSpan.FromSeconds(Interval);

        /// <summary>
        /// 获取失败消息
        /// </summary>
        protected virtual string GetFailMessage() => Type == LockType.User ? R.UserDuplicateRequest : R.GlobalDuplicateRequest;
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
}

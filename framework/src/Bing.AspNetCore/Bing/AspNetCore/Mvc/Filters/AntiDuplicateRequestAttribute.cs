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
        /// 锁类型
        /// </summary>
        public LockType Type { get; set; } = LockType.User;

        /// <summary>
        /// 再次提交时间间隔，单位：秒
        /// </summary>
        public int Interval { get; set; }

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
            var isSuccess = false;
            try
            {
                isSuccess = @lock.Lock(key, GetExpiration());
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
                if (isSuccess) 
                    @lock.UnLock();
            }
        }

        /// <summary>
        /// 创建业务锁
        /// </summary>
        /// <param name="context">操作执行上下文</param>
        private ILock CreateLock(ActionExecutingContext context) => context.HttpContext.RequestServices.GetService<ILock>() ?? NullLock.Instance;

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
            return string.IsNullOrWhiteSpace(Key) ? $"{userId}{context.HttpContext.Request.Path}" : $"{userId}{Key}";
        }

        /// <summary>
        /// 获取到期时间间隔
        /// </summary>
        private TimeSpan? GetExpiration() => Interval == 0 ? (TimeSpan?)null : TimeSpan.FromSeconds(Interval);

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

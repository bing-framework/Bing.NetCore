using System;
using System.Threading.Tasks;
using Bing.Helpers;
using Bing.Locks;
using Bing.Properties;
using Bing.Security.Sessions;
using Bing.Utils.Helpers;
using Bing.Webs.Commons;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bing.Webs.Filters
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
        /// 执行
        /// </summary>
        /// <param name="context">操作执行上下文</param>
        /// <param name="next">操作执行下一步委托</param>
        /// <returns></returns>
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (next == null)
            {
                throw new ArgumentNullException(nameof(next));
            }

            var @lock = CreateLock();
            var key = GetKey(context);
            try
            {
                var isSuccess = @lock.Lock(key);
                if (isSuccess == false)
                {
                    context.Result = new Result(StateCode.Fail, GetFailMessage());
                    return;
                }

                OnActionExecuting(context);
                if (context.Result != null)
                {
                    return;
                }

                var executedContext = await next();
                OnActionExecuted(executedContext);
            }
            finally
            {
                @lock.UnLock();
            }
        }

        /// <summary>
        /// 创建业务锁
        /// </summary>
        /// <returns></returns>
        private ILock CreateLock()
        {
            return Ioc.Create<ILock>() ?? NullLock.Instance;
        }

        /// <summary>
        /// 获取锁定标识
        /// </summary>
        /// <param name="context">操作执行上下文</param>
        /// <returns></returns>
        protected virtual string GetKey(ActionExecutingContext context)
        {
            var userId = string.Empty;
            if (Type == LockType.User)
            {
                userId = $"{Session.Instance.UserId}_";
            }

            return string.IsNullOrWhiteSpace(Key) ? $"{userId}{Web.Request.Path}" : $"{userId}{Key}";
        }

        /// <summary>
        /// 获取失败消息
        /// </summary>
        /// <returns></returns>
        protected virtual string GetFailMessage()
        {
            if (Type == LockType.User)
            {
                return R.UserDuplicateRequest;
            }

            return R.GlobalDuplicateRequest;
        }
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

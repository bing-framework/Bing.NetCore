using System;
using System.Threading.Tasks;
using Bing.Sessions;
using Bing.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.AspNetCore.Mvc
{
    /// <summary>
    /// Bing抽象控制器基类
    /// </summary>
    public abstract class BingControllerBase : ControllerBase, IAsyncResultFilter, IActionFilter, IAsyncActionFilter
    {
        /// <summary>
        /// 当前会话
        /// </summary>
        protected ISession Session { get; private set; }

        /// <summary>
        /// 当前用户
        /// </summary>
        protected ICurrentUser CurrentUser { get; private set; }

        /// <summary>
        /// 执行结果之前。在执行结果之前异步调用。
        /// </summary>
        /// <param name="context">执行结果上下文</param>
        /// <param name="next">委托</param>
        [NonAction]
        public virtual Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next) => next();

        /// <summary>
        /// 执行操作之前。在模型绑定完成之后，在执行操作之前调用。
        /// </summary>
        /// <param name="context">操作执行上下文</param>
        [NonAction]
        public virtual void OnActionExecuting(ActionExecutingContext context)
        {
        }

        /// <summary>
        /// 执行操作之后。在操作执行之后，操作结果执行之前调用。
        /// </summary>
        /// <param name="context">已执行操作上下文</param>
        [NonAction]
        public virtual void OnActionExecuted(ActionExecutedContext context)
        {
        }

        /// <summary>
        /// 执行操作之前。在模型绑定完成之后，在执行操作之前异步调用。
        /// </summary>
        /// <param name="context">执行操作上下文</param>
        /// <param name="next">委托</param>
        [NonAction]
        public virtual async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (next == null)
                throw new ArgumentNullException(nameof(next));
            Session = HttpContext.RequestServices.GetService<ISession>();
            CurrentUser = HttpContext.RequestServices.GetService<ICurrentUser>();

            OnActionExecuting(context);

            if (context.Result != null)
                return;
            OnActionExecuted(await next());
        }

        /// <summary>
        /// 返回成功消息
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="message">消息</param>
        protected virtual IActionResult Success(dynamic data = null, string message = null)
        {
            if (message == null)
                message = Bing.Properties.R.Success;
            return new ApiResult(Bing.AspNetCore.Mvc.StatusCode.Ok, message, data);
        }

        /// <summary>
        /// 返回失败消息
        /// </summary>
        /// <param name="message">消息</param>
        protected IActionResult Fail(string message) => new ApiResult(Bing.AspNetCore.Mvc.StatusCode.Fail, message);
    }
}

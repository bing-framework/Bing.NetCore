using System;
using System.Threading.Tasks;
using Bing.Logs;
using Bing.Logs.Extensions;
using Bing.Utils.Extensions;
using Bing.Utils.IO;
using Bing.Utils.Json;
using Bing.Webs.Commons;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bing.Webs.Filters
{
    /// <summary>
    /// 跟踪日志过滤器
    /// </summary>
    [AttributeUsage(AttributeTargets.Class|AttributeTargets.Method)]
    public class TraceLogAttribute : ActionFilterAttribute
    {
        /// <summary>
        /// 日志名
        /// </summary>
        public const string LogName = "ApiTraceLog";

        /// <summary>
        /// 是否忽略，为true不记录日志
        /// </summary>
        public bool Ignore { get; set; }

        /// <summary>
        /// 获取日志操作
        /// </summary>
        /// <returns></returns>
        private ILog GetLog()
        {
            try
            {
                return Log.GetLog(LogName);
            }
            catch
            {
                return Log.Null;
            }
        }

        /// <summary>
        /// 执行
        /// </summary>
        /// <param name="context">操作执行上下文</param>
        /// <param name="next">委托</param>
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

            var log = GetLog();
            OnActionExecuting(context);
            await OnActionExecutingAsync(context, log);
            if (context.Result != null)
            {
                return;
            }
            var executedContext = await next();
            OnActionExecuted(executedContext);
            OnActionExecuted(executedContext, log);
        }

        /// <summary>
        /// 执行前
        /// </summary>
        /// <param name="context">操作执行上下文</param>
        /// <param name="log">日志</param>
        protected async Task OnActionExecutingAsync(ActionExecutingContext context, ILog log)
        {
            if (Ignore)
            {
                return;
            }

            if (log.IsTraceEnabled == false)
            {
                return;
            }

            await WriteLogAsync(context, log);
        }

        /// <summary>
        /// 执行前日志
        /// </summary>
        /// <param name="context">操作执行上下文</param>
        /// <param name="log">日志</param>
        private async Task WriteLogAsync(ActionExecutingContext context, ILog log)
        {
            log.Caption("WebApi跟踪-准备执行操作")
                .Class(context.Controller.SafeString())
                .Method(context.ActionDescriptor.DisplayName);
            await AddRequestInfoAsync(context, log);
            log.Trace();
        }

        /// <summary>
        /// 添加请求信息参数
        /// </summary>
        /// <param name="context">操作执行上下文</param>
        /// <param name="log">日志</param>
        private async Task AddRequestInfoAsync(ActionExecutingContext context, ILog log)
        {
            var request = context.HttpContext.Request;
            log.Params("Http请求方式", request.Method);
            if (string.IsNullOrWhiteSpace(request.ContentType) == false)
            {
                log.Params("ContentType", request.ContentType);
            }

            AddHeaders(request, log);
            await AddFormParamsAsync(request, log);
            AddCookie(request, log);
        }

        /// <summary>
        /// 添加请求头
        /// </summary>
        /// <param name="request">Http请求</param>
        /// <param name="log">日志</param>
        private void AddHeaders(Microsoft.AspNetCore.Http.HttpRequest request, ILog log)
        {
            if (request.Headers == null || request.Headers.Count == 0)
            {
                return;
            }

            log.Params("Headers:").Params(JsonHelper.ToJson(request.Headers));
        }

        /// <summary>
        /// 添加表单参数
        /// </summary>
        /// <param name="request">Http请求</param>
        /// <param name="log">日志</param>
        private async Task AddFormParamsAsync(Microsoft.AspNetCore.Http.HttpRequest request, ILog log)
        {
            if (IsMultipart(request.ContentType))
            {
                return;
            }

            request.EnableRewind();
            var result = await FileHelper.ToStringAsync(request.Body, isCloseStream: false);
            if (string.IsNullOrWhiteSpace(result))
            {
                return;
            }

            log.Params("表单参数:").Params(result);
        }

        /// <summary>
        /// 是否multipart内容类型
        /// </summary>
        /// <param name="contentType">内容类型</param>
        private static bool IsMultipart(string contentType)
        {
            return !string.IsNullOrEmpty(contentType) && contentType.IndexOf("multipart/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 添加Cookie
        /// </summary>
        /// <param name="request">Http请求</param>
        /// <param name="log">日志</param>
        private void AddCookie(Microsoft.AspNetCore.Http.HttpRequest request, ILog log)
        {
            log.Params("Cookie:");
            foreach (var key in request.Cookies.Keys)
            {
                log.Params(key, request.Cookies[key]);
            }
        }

        /// <summary>
        /// 执行后
        /// </summary>
        /// <param name="context">操作执行上下文</param>
        /// <param name="log">日志</param>
        public virtual void OnActionExecuted(ActionExecutedContext context, ILog log)
        {
            if (Ignore)
            {
                return;
            }

            if (log.IsTraceEnabled == false)
            {
                return;
            }

            WriteLog(context, log);
        }

        /// <summary>
        /// 执行后的日志
        /// </summary>
        /// <param name="context">操作执行上下文</param>
        /// <param name="log">日志</param>
        private void WriteLog(ActionExecutedContext context, ILog log)
        {
            log.Caption("WebApi跟踪-执行操作完成")
                .Class(context.Controller.SafeString())
                .Method(context.ActionDescriptor.DisplayName);
            AddResponseInfo(context, log);
            AddResult(context, log);
            log.Trace();
        }

        /// <summary>
        /// 添加响应信息参数
        /// </summary>
        /// <param name="context">操作执行上下文</param>
        /// <param name="log">日志</param>
        private void AddResponseInfo(ActionExecutedContext context, ILog log)
        {
            var response = context.HttpContext.Response;
            if (string.IsNullOrWhiteSpace(response.ContentType) == false)
            {
                log.Content($"ContentType: {response.ContentType}");
            }

            log.Content($"Http状态码: {response.StatusCode}");
        }

        /// <summary>
        /// 记录响应结果
        /// </summary>
        /// <param name="context">操作执行上下文</param>
        /// <param name="log">日志</param>
        private void AddResult(ActionExecutedContext context, ILog log)
        {
            if (!(context.Result is Result result))
            {
                return;
            }

            log.Content($"响应消息: {result.Message}")
                .Content("响应结果:")
                .Content($"{JsonHelper.ToJson(result.Data)}");
        }
    }
}

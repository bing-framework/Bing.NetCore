using System;
using Bing.Logs;
using Bing.Logs.Extensions;
using Bing.Utils.Extensions;
using Bing.Utils.IO;
using Bing.Utils.Json;
using Bing.Webs.Commons;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bing.Webs.Filters
{
    /// <summary>
    /// 跟踪日志过滤器
    /// </summary>
    [AttributeUsage(AttributeTargets.Class|AttributeTargets.Method)]
    public class TraceLogAttribute:ActionFilterAttribute
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
        /// 日志
        /// </summary>
        private ILog Logger { get; set; }

        /// <summary>
        /// 执行前
        /// </summary>
        /// <param name="context">操作执行上下文</param>
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);
            if (Ignore)
            {
                return;
            }
            Logger = GetLog();
            if (Logger.IsTraceEnabled == false)
            {
                return;
            }
            WriteLog(context);
        }

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
        /// 执行前日志
        /// </summary>
        /// <param name="context">操作执行上下文</param>
        private void WriteLog(ActionExecutingContext context)
        {
            Logger.Caption("WebApi跟踪-准备执行操作")
                .Class(context.Controller.SafeString())
                .Method(context.ActionDescriptor.DisplayName);
            AddRequestInfo(context);
            Logger.Trace();
        }

        /// <summary>
        /// 添加请求信息参数
        /// </summary>
        /// <param name="context">操作执行上下文</param>
        private void AddRequestInfo(ActionExecutingContext context)
        {
            var request = context.HttpContext.Request;
            Logger.Params("Http请求方式", request.Method);
            if (string.IsNullOrWhiteSpace(request.ContentType) == false)
            {
                Logger.Params("ContentType", request.ContentType);
            }
            AddHeaders(request);
            AddFormParams(request);
            AddCookie(request);
        }

        /// <summary>
        /// 添加请求头
        /// </summary>
        /// <param name="request">Http请求</param>
        private void AddHeaders(Microsoft.AspNetCore.Http.HttpRequest request)
        {
            Logger.Params("请求头:");
            foreach (var header in request.Headers)
            {
                Logger.Params(header.Key, header.Value);
            }
        }

        /// <summary>
        /// 添加表单参数
        /// </summary>
        /// <param name="request">Http请求</param>
        private void AddFormParams(Microsoft.AspNetCore.Http.HttpRequest request)
        {
            if (IsMultipart(request.ContentType))
            {
                return;
            }
            var result = FileUtil.ToString(request.Body);
            if (string.IsNullOrWhiteSpace(result))
            {
                return;
            }
            Logger.Params("表单参数:").Params(result);
        }

        /// <summary>
        /// 是否multipart内容类型
        /// </summary>
        /// <param name="contentType">内容类型</param>
        /// <returns></returns>
        private static bool IsMultipart(string contentType)
        {
            return !string.IsNullOrEmpty(contentType) && contentType.IndexOf("multipart/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 添加Cookie
        /// </summary>
        /// <param name="request">Http请求</param>
        private void AddCookie(Microsoft.AspNetCore.Http.HttpRequest request)
        {
            Logger.Params("Cookie:");
            foreach (var key in request.Cookies.Keys)
            {
                Logger.Params(key, request.Cookies[key]);
            }
        }

        /// <summary>
        /// 执行后
        /// </summary>
        /// <param name="context">结果执行上下文</param>
        public override void OnResultExecuted(ResultExecutedContext context)
        {
            base.OnResultExecuted(context);
            if (Ignore)
            {
                return;
            }
            if (Logger.IsTraceEnabled == false)
            {
                return;
            }
            WriteLog(context);
        }

        /// <summary>
        /// 执行后的日志
        /// </summary>
        /// <param name="context">结果执行上下文</param>
        private void WriteLog(ResultExecutedContext context)
        {
            Logger.Caption("WebApi跟踪-执行操作完成")
                .Class(context.Controller.SafeString())
                .Method(context.ActionDescriptor.DisplayName);
            AddResponseInfo(context);
            AddResult(context);
            Logger.Trace();
        }

        /// <summary>
        /// 添加响应信息参数
        /// </summary>
        /// <param name="context">结果执行上下文</param>
        private void AddResponseInfo(ResultExecutedContext context)
        {
            var response = context.HttpContext.Response;
            if (string.IsNullOrWhiteSpace(response.ContentType) == false)
            {
                Logger.Content($"ContentType: {response.ContentType}");
            }
            Logger.Content($"Http状态码: {response.StatusCode}");
        }

        /// <summary>
        /// 记录响应结果
        /// </summary>
        /// <param name="context">结果执行上下文</param>
        private void AddResult(ResultExecutedContext context)
        {
            if (!(context.Result is Result result))
            {
                return;
            }
            Logger.Content($"响应消息: {result.Message}")
                .Content("响应结果:")
                .Content($"{JsonUtil.ToJson(result.Data)}");
        }

        
    }
}

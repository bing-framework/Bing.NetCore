using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bing.AspNetCore.Logs
{
    /// <summary>
    /// 请求响应记录器中间件
    /// </summary>
    public class RequestResponseLoggerMiddleware : IMiddleware
    {
        /// <summary>
        /// 方法
        /// </summary>
        private readonly RequestDelegate _next;

        /// <summary>
        /// 请求响应记录器选项
        /// </summary>
        private readonly RequestResponseLoggerOptions _options;

        /// <summary>
        /// 请求响应记录器
        /// </summary>
        private readonly IRequestResponseLogger _logger;

        /// <summary>
        /// 初始化一个<see cref="RequestResponseLoggerMiddleware"/>类型的实例
        /// </summary>
        /// <param name="next">方法</param>
        /// <param name="options">请求响应记录器选项</param>
        /// <param name="logger">请求响应记录器</param>
        public RequestResponseLoggerMiddleware(RequestDelegate next, IOptions<RequestResponseLoggerOptions> options, IRequestResponseLogger logger)
        {
            _next = next;
            _options = options.Value;
            _logger = logger;
        }

        /// <summary>
        /// 执行中间件拦截逻辑
        /// </summary>
        /// <param name="context">Http上下文</param>
        public async Task InvokeAsync(HttpContext context)
        {
            if (_options == null || !_options.IsEnabled || FilterRequest(context))
            {
                await _next(context);
                return;
            }
            var logCreator = context?.RequestServices?.GetRequiredService<IRequestResponseLogCreator>();
            var log = logCreator?.Log;
            if (log == null)
            {
                await _next(context);
                return;
            }

            log.RequestDateTimeUtc = DateTime.UtcNow;
            var request = context.Request;

            // log
            log.LogId = Guid.NewGuid().ToString();
            log.TraceId = context.TraceIdentifier;
            var ip = request.HttpContext.Connection.RemoteIpAddress;
            log.ClientIp = ip?.ToString();
            log.Node = _options.Name;

            // request
            log.RequestMethod = request.Method;
            log.RequestPath = request.Path;
            log.RequestQuery = request.QueryString.ToString();
            log.RequestQueries = FormatQueries(request.QueryString.ToString());
            log.RequestHeaders = _options.WithHeader ? FormatHeaders(request.Headers) : null;
            log.RequestCookies = _options.WithCookie ? FormatCookies(request.Cookies) : null;
            log.RequestBody = await ReadBodyFromRequest(request);
            log.RequestScheme = request.Scheme;
            log.RequestHost = request.Host.ToString();
            log.RequestContentType = request.ContentType;

            // 暂时用 MemoryStream 替换 HttpResponseStream，用于获取其运行中的值
            var response = context.Response;
            var originalResponseBody = response.Body;
            using var newResponseBody = new MemoryStream();
            response.Body = newResponseBody;

            // 调用管道中的下一个中间件
            try
            {
                await _next(context);
            }
            catch (Exception e)
            {
                // 异常：app.UseExceptionHandler() 或者 中间件
                LogError(log, e);
            }

            newResponseBody.Seek(0, SeekOrigin.Begin);
            var responseBodyText = await ReadBodyFromResponse(context);
            newResponseBody.Seek(0, SeekOrigin.Begin);
            
            await newResponseBody.CopyToAsync(originalResponseBody);

            // response
            log.ResponseContentType = response.ContentType;
            log.ResponseStatus = response.StatusCode.ToString();
            log.ResponseHeaders = _options.WithHeader ? FormatHeaders(response.Headers) : null;
            log.ResponseBody = _options.WithResponse ? responseBodyText : null;
            log.ResponseDateTimeUtc = DateTime.UtcNow;

#if NETCOREAPP3_1_OR_GREATER
            var contextFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
            if (contextFeature != null && contextFeature.Error != null)
            {
                var exception = contextFeature.Error;
                LogError(log, exception);
            }
#endif

            _logger.Log(logCreator);
        }

        /// <summary>
        /// 记录错误
        /// </summary>
        /// <param name="log">请求响应日志</param>
        /// <param name="exception">异常</param>
        private void LogError(RequestResponseLog log, Exception exception)
        {
            log.ExceptionMessage = exception.Message;
            log.ExceptionStackTrace = exception.StackTrace;
        }

        /// <summary>
        /// 格式化请求头
        /// </summary>
        /// <param name="headers">请求头</param>
        private Dictionary<string, string> FormatHeaders(IHeaderDictionary headers)
        {
            var pairs = new Dictionary<string, string>();
            foreach (var header in headers)
                pairs.Add(header.Key, header.Value);
            return pairs;
        }

        /// <summary>
        /// 格式化Cookies
        /// </summary>
        /// <param name="cookies">Cookie集合</param>
        private Dictionary<string, string> FormatCookies(IRequestCookieCollection cookies)
        {
            var pairs = new Dictionary<string, string>();
            foreach (var cookie in cookies) 
                pairs.Add(cookie.Key, cookie.Value);
            return pairs;
        }

        /// <summary>
        /// 格式化查询字符串
        /// </summary>
        /// <param name="queryString">查询字符串</param>
        private List<KeyValuePair<string, string>> FormatQueries(string queryString)
        {
            var pairs = new List<KeyValuePair<string, string>>();
            foreach (var query in queryString.TrimStart('?').Split('&'))
            {
                var items = query.Split('=');
                var key = items.Any() ? items[0] : string.Empty;
                var value = items.Length >= 2 ? items[1] : string.Empty;
                if (!string.IsNullOrEmpty(key))
                    pairs.Add(new KeyValuePair<string, string>(key, value));
            }
            return pairs;
        }

        /// <summary>
        /// 从请求中读取正文内容
        /// </summary>
        /// <param name="request">Http请求</param>
        private async Task<string> ReadBodyFromRequest(HttpRequest request)
        {
            // 确保可以多次读取请求的正文，用于管道中的下一个中间件
            request.EnableBuffering();
#if NETCOREAPP3_1_OR_GREATER
            using var streamReader = new StreamReader(request.Body, leaveOpen: true);
#else
            using var streamReader = new StreamReader(request.Body, System.Text.Encoding.UTF8, true, 1024, true);
#endif
            var requestBody = await streamReader.ReadToEndAsync();
            // 重置请求的主体流位置，用于管道中的下一个中间件
            request.Body.Position = 0;
            return requestBody;
        }

        /// <summary>
        /// 从响应中读取正文内容
        /// </summary>
        /// <param name="context">Http上下文</param>
        private async Task<string> ReadBodyFromResponse(HttpContext context)
        {
            if (string.IsNullOrWhiteSpace(context.Response.ContentType) || !_options.WithResponse)
            {
                if (context.Response.Body.CanSeek)
                    context.Response.Body.Seek(0, SeekOrigin.Begin);
                return string.Empty;
            }

            if (FilterStaticFiles(context))
            {
                if (context.Response.Body.CanSeek)
                    context.Response.Body.Seek(0, SeekOrigin.Begin);
                return string.Empty;
            }

            var result = string.Empty;
            context.Response.Body.Seek(0, SeekOrigin.Begin);

            Stream source = null;
            if (context.Response.Headers.ContainsKey("Content-Encoding"))
            {
                var contentEncoding = context.Response.Headers["Content-Encoding"].ToString();
                switch (contentEncoding)
                {
                    case "gzip":
                        source = new GZipStream(context.Response.Body, CompressionMode.Decompress);
                        break;
                    case "deflate":
                        source = new DeflateStream(context.Response.Body, CompressionMode.Decompress);
                        break;
#if NETCOREAPP3_1_OR_GREATER
                    case "br":
                        source = new BrotliStream(context.Response.Body, CompressionMode.Decompress);
                        break;
#endif
                }
            }

            source ??= context.Response.Body;

            var responseBodyText = await new StreamReader(source, Encoding.UTF8).ReadToEndAsync();
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            return responseBodyText;
        }

        /// <summary>
        /// 过滤静态文件
        /// </summary>
        /// <param name="context">Http上下文</param>
        private bool FilterStaticFiles(HttpContext context)
        {
            if (!string.IsNullOrWhiteSpace(context.Request.ContentType) && context.Request.ContentType.Contains("application/grpc"))
                return false;
            if (context.Request.Method.ToLowerInvariant() == "options")
                return true;
            if (context.Request.Path.HasValue && context.Request.Path.Value.Contains("."))
                return true;
            return false;
        }

        /// <summary>
        /// 过滤请求
        /// </summary>
        /// <param name="context">Http上下文</param>
        private bool FilterRequest(HttpContext context)
        {
            // 过滤上传文件请求
            if (context.Request.HasFormContentType && context.Request.Form != null && context.Request.Form.Files != null && context.Request.Form.Files.Any())
                return true;
            // 过滤请求数据
            if (_options.RequestFilter == null || _options.RequestFilter.Count == 0)
                return false;
            return false;
        }
    }
}

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Bing.Utils.Extensions;
using Bing.Utils.Helpers;
using Bing.Utils.Json;

namespace Bing.Utils.Webs.Clients
{
    /// <summary>
    /// Http请求
    /// </summary>
    public class HttpRequest:HttpRequestBase<IHttpRequest>,IHttpRequest
    {
        /// <summary>
        /// 执行成功的回调函数
        /// </summary>
        private Action<string> _successAction;

        /// <summary>
        /// 执行成功的回调函数
        /// </summary>
        private Action<string, HttpStatusCode> _successStatusCodeAction;

        /// <summary>
        /// 初始化一个<see cref="HttpRequest"/>类型的实例
        /// </summary>
        /// <param name="httpMethod">Http请求方法</param>
        /// <param name="url">请求地址</param>
        public HttpRequest(HttpMethod httpMethod, string url) : base(httpMethod, url)
        {
        }

        /// <summary>
        /// 请求成功回调函数
        /// </summary>
        /// <param name="action">执行成功的回调函数，参数为响应结果</param>
        /// <returns></returns>
        public IHttpRequest OnSuccess(Action<string> action)
        {
            _successAction = action;
            return this;
        }

        /// <summary>
        /// 请求成功回调函数
        /// </summary>
        /// <param name="action">执行成功的回调函数，第一个参数为响应结果，第二个参数为状态码</param>
        /// <returns></returns>
        public IHttpRequest OnSuccess(Action<string, HttpStatusCode> action)
        {
            _successStatusCodeAction = action;
            return this;
        }

        /// <summary>
        /// 成功处理操作
        /// </summary>
        /// <param name="result">结果</param>
        /// <param name="statusCode">状态码</param>
        /// <param name="contentType">内容类型</param>
        protected override void SuccessHandler(string result, HttpStatusCode statusCode, string contentType)
        {
            _successAction?.Invoke(result);
            _successStatusCodeAction?.Invoke(result, statusCode);
        }

        /// <summary>
        /// 获取Json结果
        /// </summary>
        /// <typeparam name="TResult">返回结果类型</typeparam>
        /// <returns></returns>
        public async Task<TResult> ResultFromJsonAsync<TResult>()
        {
            return JsonHelper.ToObject<TResult>(await ResultAsync());
        }
    }

    /// <summary>
    /// Http请求
    /// </summary>
    /// <typeparam name="TResult">结果类型</typeparam>
    public class HttpRequest<TResult> : HttpRequestBase<IHttpRequest<TResult>>, IHttpRequest<TResult>
        where TResult : class
    {
        /// <summary>
        /// 执行成功的回调函数
        /// </summary>
        private Action<TResult> _successAction;

        /// <summary>
        /// 执行成功的回调函数
        /// </summary>
        private Action<TResult, HttpStatusCode> _successStatusCodeAction;

        /// <summary>
        /// 执行成功的转换函数
        /// </summary>
        private Func<string, TResult> _convertAction;

        /// <summary>
        /// 初始化一个<see cref="HttpRequest{TResult}"/>类型的实例
        /// </summary>
        /// <param name="httpMethod">Http请求方法</param>
        /// <param name="url">请求地址</param>
        public HttpRequest(HttpMethod httpMethod, string url) : base(httpMethod, url)
        {
        }

        /// <summary>
        /// 请求成功回调函数
        /// </summary>
        /// <param name="action">执行成功的回调函数，参数为响应结果</param>
        /// <param name="convertAction">将结果字符串转换为指定类型，当默认转换实现无法转换时使用</param>
        /// <returns></returns>
        public IHttpRequest<TResult> OnSuccess(Action<TResult> action, Func<string, TResult> convertAction = null)
        {
            _successAction = action;
            _convertAction = convertAction;
            return this;
        }

        /// <summary>
        /// 请求成功回调函数
        /// </summary>
        /// <param name="action">执行成功的回调函数，第一个参数为响应结果，第二个参数为状态码</param>
        /// <param name="convertAction">将结果字符串转换为指定类型，当默认转换实现无法转换时使用</param>
        /// <returns></returns>
        public IHttpRequest<TResult> OnSuccess(Action<TResult, HttpStatusCode> action, Func<string, TResult> convertAction = null)
        {
            _successStatusCodeAction = action;
            _convertAction = convertAction;
            return this;
        }

        /// <summary>
        /// 成功处理操作
        /// </summary>
        /// <param name="result">结果</param>
        /// <param name="statusCode">状态码</param>
        /// <param name="contentType">内容类型</param>
        protected override void SuccessHandler(string result, HttpStatusCode statusCode, string contentType)
        {
            TResult successResult = ConvertTo(result, contentType);
            _successAction?.Invoke(successResult);
            _successStatusCodeAction?.Invoke(successResult,statusCode);
        }

        /// <summary>
        /// 将结果字符串转换为指定类型
        /// </summary>
        /// <param name="result">结果</param>
        /// <param name="contentType">内容类型</param>
        /// <returns></returns>
        private TResult ConvertTo(string result, string contentType)
        {
            if (typeof(TResult) == typeof(string))
            {
                return Conv.To<TResult>(result);
            }
            if (_convertAction != null)
            {
                return _convertAction(result);
            }
            if (contentType.SafeString().ToLower() == "application/json")
            {
                return JsonHelper.ToObject<TResult>(result);
            }
            return null;
        }

        /// <summary>
        /// 获取Json结果
        /// </summary>
        /// <returns></returns>
        public async Task<TResult> ResultFromJsonAsync()
        {
            return JsonHelper.ToObject<TResult>(await ResultAsync());
        }
    }
}

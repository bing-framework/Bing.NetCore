using System;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Http;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Extensions
{
    /// <summary>
    /// Http请求(<see cref="HttpRequest"/>) 扩展
    /// </summary>
    public static class HttpRequestExtensions
    {
        /// <summary>
        /// 获取Http请求的绝对路径
        /// </summary>
        /// <param name="request">Http请求</param>
        public static string GetAbsoluteUri(this HttpRequest request) => new StringBuilder()
            .Append(request.Scheme)
            .Append("://")
            .Append(request.Host)
            .Append(request.PathBase)
            .Append(request.Path)
            .Append(request.QueryString)
            .ToString();

        /// <summary>
        /// 获取查询参数
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="request">Http请求</param>
        /// <param name="key">键</param>
        /// <param name="defaultValue">默认值</param>
        public static T Query<T>(this HttpRequest request, string key, T defaultValue = default)
            where T : IConvertible
        {
            var value = request.Query.FirstOrDefault(x => x.Key == key);
            if (string.IsNullOrWhiteSpace(value.Value.ToString()))
            {
                return defaultValue;
            }

            try
            {
                return (T)Convert.ChangeType(value.Value.ToString(), typeof(T));
            }
            catch (InvalidCastException)
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// 获取表单参数
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="request">Http请求</param>
        /// <param name="key">键</param>
        /// <param name="defaultValue">默认值</param>
        public static T Form<T>(this HttpRequest request, string key, T defaultValue = default)
            where T : IConvertible
        {
            var value = request.Form.FirstOrDefault(x => x.Key == key);
            if (string.IsNullOrWhiteSpace(value.Value.ToString()))
            {
                return defaultValue;
            }

            try
            {
                return (T)Convert.ChangeType(value.Value.ToString(), typeof(T));
            }
            catch (InvalidCastException)
            {
                return defaultValue;
            }
        }
    }
}

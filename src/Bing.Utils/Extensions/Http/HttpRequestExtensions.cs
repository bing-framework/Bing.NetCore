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
        #region GetAbsoluteUri(获取Http请求的绝对路径)

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

        #endregion

        #region Query(获取查询参数)

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
                return defaultValue;
            try
            {
                return (T)Convert.ChangeType(value.Value.ToString(), typeof(T));
            }
            catch (InvalidCastException)
            {
                return defaultValue;
            }
        }

        #endregion

        #region Form(获取表单参数)

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
                return defaultValue;
            try
            {
                return (T)Convert.ChangeType(value.Value.ToString(), typeof(T));
            }
            catch (InvalidCastException)
            {
                return defaultValue;
            }
        }

        #endregion

        #region Params(获取参数)

        /// <summary>
        /// 获取参数
        /// </summary>
        /// <param name="request">请求信息</param>
        /// <param name="key">键名</param>
        public static string Params(this HttpRequest request, string key)
        {
            if (request.Query.ContainsKey(key))
            {
                return request.Query[key];
            }

            if (request.HasFormContentType)
            {
                return request.Form[key];
            }

            return null;
        }

        #endregion

        #region IsAjaxRequest(是否Ajax请求)

        /// <summary>
        /// 是否Ajax请求
        /// </summary>
        /// <param name="request">Http请求</param>
        public static bool IsAjaxRequest(this HttpRequest request)
        {
            request.CheckNotNull(nameof(request));
            bool? flag = request.Headers?["X-Requested-With"].ToString()
                ?.Equals("XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
            return flag.HasValue && flag.Value;
        }

        #endregion

        #region IsJsonContentType(是否Json内容类型)

        /// <summary>
        /// 是否Json内容类型
        /// </summary>
        /// <param name="request">Http请求</param>
        public static bool IsJsonContentType(this HttpRequest request)
        {
            request.CheckNotNull(nameof(request));
            bool flag =
                request.Headers?["Content-Type"].ToString()
                    .IndexOf("application/json", StringComparison.OrdinalIgnoreCase) > -1 || request
                    .Headers?["Content-Type"].ToString().IndexOf("text/json", StringComparison.OrdinalIgnoreCase) > -1;

            if (flag)
                return true;
            flag =
                request.Headers?["Accept"].ToString().IndexOf("application/json", StringComparison.OrdinalIgnoreCase) >
                -1 || request.Headers?["Accept"].ToString().IndexOf("text/json", StringComparison.OrdinalIgnoreCase) >
                -1;
            return flag;
        }

        #endregion
    }
}

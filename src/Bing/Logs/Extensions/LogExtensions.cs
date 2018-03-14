using System;
using System.Collections.Generic;
using System.Text;
using Bing.Logs.Abstractions;

namespace Bing.Logs.Extensions
{
    /// <summary>
    /// 日志操作 扩展
    /// </summary>
    public static class LogExtensions
    {
        /// <summary>
        /// 设置内容
        /// </summary>
        /// <param name="log">日志操作</param>
        /// <returns></returns>
        public static ILog Content(this ILog log)
        {
            return log.Set<ILogContent>(content => content.Content(""));
        }

        /// <summary>
        /// 设置内容并换行
        /// </summary>
        /// <param name="log">日志操作</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static ILog Content(this ILog log, string value)
        {
            return log.Set<ILogContent>(content => content.Content(value));
        }

        /// <summary>
        /// 设置内容
        /// </summary>
        /// <param name="log">日志操作</param>
        /// <param name="dictionary">字典</param>
        /// <returns></returns>
        public static ILog Content(this ILog log, IDictionary<string, string> dictionary)
        {
            if (dictionary == null)
            {
                return log;
            }
            foreach (var keyValue in dictionary)
            {
                log.Set<ILogContent>(content => content.Content($"{keyValue.Key} : {keyValue.Value}"));
            }
            return log;
        }
    }
}

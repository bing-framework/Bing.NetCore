using System.Text;
using Bing.Logs.Abstractions;
using Bing.Utils.Extensions;

namespace Bing.Logs.Extensions
{
    /// <summary>
    /// 日志内容 扩展
    /// </summary>
    public static class LogContentExtensions
    {
        /// <summary>
        /// 追加内容
        /// </summary>
        /// <param name="content">日志内容</param>
        /// <param name="result">拼接字符串</param>
        /// <param name="value">值</param>
        public static void Append(this ILogContent content, StringBuilder result, string value)
        {
            if (value.IsEmpty())
            {
                return;
            }
            result.Append(value);
        }

        /// <summary>
        /// 追加内容并换行
        /// </summary>
        /// <param name="content">日志内容</param>
        /// <param name="result">拼接字符串</param>
        /// <param name="value">值</param>
        public static void AppendLine(this ILogContent content, StringBuilder result, string value)
        {
            content.Append(result, value);
            result.AppendLine();
        }

        /// <summary>
        /// 设置内容并换行
        /// </summary>
        /// <param name="content">日志内容</param>
        /// <param name="value">值</param>
        public static void Content(this ILogContent content, string value)
        {
            content.AppendLine(content.Content, value);
        }
    }
}

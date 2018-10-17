using System;
using System.Collections.Generic;
using System.Text;
using Bing.Properties;
using Bing.Utils.Extensions;
using Bing.Utils.Helpers;

namespace Bing.Exceptions.Prompts
{
    /// <summary>
    /// 异常提示
    /// </summary>
    public static class ExceptionPrompt
    {
        /// <summary>
        /// 异常提示组件集合
        /// </summary>
        private static readonly List<IExceptionPrompt> Prompts=new List<IExceptionPrompt>();

        /// <summary>
        /// 添加异常提示
        /// </summary>
        /// <param name="prompt">异常提示</param>
        public static void AddPrompt(IExceptionPrompt prompt)
        {
            if (prompt == null)
            {
                throw new ArgumentNullException(nameof(prompt));
            }

            if (Prompts.Contains(prompt))
            {
                return;
            }
            Prompts.Add(prompt);
        }

        /// <summary>
        /// 获取异常提示
        /// </summary>
        /// <param name="exception">异常</param>
        /// <returns></returns>
        public static string GetPrompty(Exception exception)
        {
            exception = exception.GetRawException();
            var prompt = GetExceptionPrompt(exception);
            if (string.IsNullOrWhiteSpace(prompt) == false)
            {
                return prompt;
            }

            if (exception is Warning warning)
            {
                return Filter(warning.Message);
            }

            return R.SystemError;
        }

        /// <summary>
        /// 获取异常提示
        /// </summary>
        /// <param name="exception">提倡</param>
        /// <returns></returns>
        private static string GetExceptionPrompt(Exception exception)
        {
            foreach (var prompt in Prompts)
            {
                var result = prompt.GetPrompt(exception);
                if (result.IsEmpty() == false)
                {
                    return result;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// 过滤无用消息
        /// </summary>
        /// <param name="message">消息</param>
        /// <returns></returns>
        private static string Filter(string message)
        {
            return message.Replace($"{Common.Line}@exceptionless:{Common.Line}", "");
        }
    }
}

using System;

namespace Bing.Offices.Core.Errors
{
    /// <summary>
    /// Excel 错误
    /// </summary>
    public class ExcelError
    {
        /// <summary>
        /// 错误消息
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// 异常信息
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// 初始一个<see cref="ExcelError"/>类型的实例
        /// </summary>
        /// <param name="errorMessage">错误消息</param>
        public ExcelError(string errorMessage)
        {
            ErrorMessage = errorMessage ?? string.Empty;
        }

        /// <summary>
        /// 初始一个<see cref="ExcelError"/>类型的实例
        /// </summary>
        /// <param name="exception">异常</param>
        public ExcelError(Exception exception) : this(exception.Message, exception) { }

        /// <summary>
        /// 初始一个<see cref="ExcelError"/>类型的实例
        /// </summary>
        /// <param name="errorMessage">错误消息</param>
        /// <param name="exception">异常</param>
        public ExcelError(string errorMessage, Exception exception) : this(errorMessage)
        {
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }
    }
}

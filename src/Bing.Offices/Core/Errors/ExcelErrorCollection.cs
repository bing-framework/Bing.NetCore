using System;
using System.Collections.ObjectModel;

namespace Bing.Offices.Core.Errors
{
    /// <summary>
    /// Excel错误集合
    /// </summary>
    public class ExcelErrorCollection:Collection<ExcelError>
    {
        /// <summary>
        /// 添加错误
        /// </summary>
        /// <param name="errorMessage">错误消息</param>
        public void Add(string errorMessage)
        {
            Add(new ExcelError(errorMessage));
        }

        /// <summary>
        /// 添加错误
        /// </summary>
        /// <param name="exception">异常</param>
        public void Add(Exception exception)
        {
            Add(new ExcelError(exception));
        }
    }
}

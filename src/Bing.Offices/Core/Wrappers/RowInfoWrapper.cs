using System;
using Bing.Offices.Core.Errors;

namespace Bing.Offices.Core.Wrappers
{
    /// <summary>
    /// 行信息包
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    public class RowInfoWrapper<T> where T:class,new()
    {
        /// <summary>
        /// 行索引
        /// </summary>
        public int? RowIndex { get; }

        /// <summary>
        /// 数据
        /// </summary>
        public T Data { get; } = new T();

        /// <summary>
        /// 错误字典
        /// </summary>
        public ExcelErrorDictionary ErrorDict { get; }

        /// <summary>
        /// 初始化一个<see cref="RowInfoWrapper{T}"/>类型的实例
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        public RowInfoWrapper(int rowIndex)
        {
            RowIndex = rowIndex;
            ErrorDict = new ExcelErrorDictionary();
        }

        /// <summary>
        /// 添加错误
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="errorMessage">错误消息</param>
        /// <param name="columnIndex">列索引</param>
        public void AddError(string key, string errorMessage, int columnIndex) => ErrorDict.AddError(key, errorMessage, columnIndex, RowIndex);

        /// <summary>
        /// 添加错误
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="exception">异常</param>
        /// <param name="columnIndex">列索引</param>
        public void AddError(string key, Exception exception, int columnIndex) => ErrorDict.AddError(key, exception, columnIndex, RowIndex);

        /// <summary>
        /// 添加错误
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="info">错误信息</param>
        public void AddError(string key, ExcelErrorInfo info) => ErrorDict.Add(key, info);
    }
}

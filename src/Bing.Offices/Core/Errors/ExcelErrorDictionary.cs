using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Bing.Offices.Core.Errors
{
    /// <summary>
    /// Excel 错误字典
    /// </summary>
    public class ExcelErrorDictionary:IDictionary<string,ExcelErrorInfo>
    {
        /// <summary>
        /// 内部字典
        /// </summary>
        private readonly Dictionary<string,ExcelErrorInfo> _innerDictionary=new Dictionary<string, ExcelErrorInfo>();

        /// <summary>
        /// 索引器
        /// </summary>
        /// <param name="key">键名</param>
        /// <returns></returns>
        public ExcelErrorInfo this[string key]
        {
            get
            {
                _innerDictionary.TryGetValue(key, out var value);
                return value;
            }
            set => _innerDictionary[key] = value;
        }

        /// <summary>
        /// 字典总数
        /// </summary>
        public int Count => _innerDictionary.Count;

        /// <summary>
        /// 是否只读字典
        /// </summary>
        public bool IsReadOnly => ((IDictionary<string, ExcelErrorInfo>)_innerDictionary).IsReadOnly;

        /// <summary>
        /// 是否验证通过
        /// </summary>
        public bool IsValid => Values.All(x => x.Errors.Count == 0);

        /// <summary>
        /// 键名集合
        /// </summary>
        public ICollection<string> Keys => _innerDictionary.Keys;

        /// <summary>
        /// 键值集合
        /// </summary>
        public ICollection<ExcelErrorInfo> Values => _innerDictionary.Values;

        /// <summary>
        /// 获取枚举器
        /// </summary>
        /// <returns></returns>
        public IEnumerator<KeyValuePair<string, ExcelErrorInfo>> GetEnumerator() => _innerDictionary.GetEnumerator();

        /// <summary>
        /// 获取枚举器
        /// </summary>
        /// <returns></returns>
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_innerDictionary).GetEnumerator();

        /// <summary>
        /// 添加键值对
        /// </summary>
        /// <param name="item">键值对</param>
        public void Add(KeyValuePair<string, ExcelErrorInfo> item) => ((IDictionary<string, ExcelErrorInfo>)_innerDictionary).Add(item);

        /// <summary>
        /// 添加键值对
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="value">键值</param>
        public void Add(string key, ExcelErrorInfo value) => _innerDictionary.Add(key, value);

        /// <summary>
        /// 清空字典
        /// </summary>
        public void Clear() => _innerDictionary.Clear();

        /// <summary>
        /// 是否包含指定键值对
        /// </summary>
        /// <param name="item">键值对</param>
        /// <returns></returns>
        public bool Contains(KeyValuePair<string, ExcelErrorInfo> item) => ((IDictionary<string, ExcelErrorInfo>)_innerDictionary).Contains(item);

        /// <summary>
        /// 是否存在指定键名
        /// </summary>
        /// <param name="key">键名</param>
        /// <returns></returns>
        public bool ContainsKey(string key) => _innerDictionary.ContainsKey(key);

        /// <summary>
        /// 复制键值对数组到指定数组索引处
        /// </summary>
        /// <param name="array">键值对数组</param>
        /// <param name="arrayIndex">数组索引</param>
        public void CopyTo(KeyValuePair<string, ExcelErrorInfo>[] array, int arrayIndex) => ((IDictionary<string, ExcelErrorInfo>)_innerDictionary).CopyTo(array, arrayIndex);

        /// <summary>
        /// 移除键值对
        /// </summary>
        /// <param name="item">键值对</param>
        /// <returns></returns>
        public bool Remove(KeyValuePair<string, ExcelErrorInfo> item) => ((IDictionary<string, ExcelErrorInfo>)_innerDictionary).Remove(item);

        /// <summary>
        /// 移除指定键名的键值对
        /// </summary>
        /// <param name="key">键名</param>
        /// <returns></returns>
        public bool Remove(string key) => _innerDictionary.Remove(key);

        /// <summary>
        /// 尝试获取指定键名的值
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="value">键值</param>
        /// <returns></returns>
        public bool TryGetValue(string key, out ExcelErrorInfo value) => _innerDictionary.TryGetValue(key, out value);

        /// <summary>
        /// 根据键名获取错误信息
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="columnIndex">列索引</param>
        /// <param name="rowIndex">行索引</param>
        /// <returns></returns>
        public ExcelErrorInfo GetErrorForKey(string key, int? columnIndex = null, int? rowIndex = null)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (!TryGetValue(key, out var excelErrorInfo))
            {
                excelErrorInfo = new ExcelErrorInfo() {ColumnIndex = columnIndex, RowIndex = rowIndex};
                this[key] = excelErrorInfo;
            }

            return excelErrorInfo;
        }

        /// <summary>
        /// 添加错误
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="exception">异常</param>
        /// <param name="columnIndex">列索引</param>
        /// <param name="rowIndex">行索引</param>
        public void AddError(string key, Exception exception, int columnIndex, int? rowIndex = null) =>
            GetErrorForKey(key, columnIndex, rowIndex).Errors.Add(exception);

        /// <summary>
        /// 添加错误
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="errorMessage">错误消息</param>
        /// <param name="columnIndex">列索引</param>
        /// <param name="rowIndex">行索引</param>
        public void AddError(string key, string errorMessage, int columnIndex, int? rowIndex = null) =>
            GetErrorForKey(key, columnIndex, rowIndex).Errors.Add(errorMessage);
    }
}

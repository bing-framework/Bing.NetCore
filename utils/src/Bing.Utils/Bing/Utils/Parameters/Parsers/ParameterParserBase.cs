using System;
using System.Collections.Generic;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Utils.Json;

namespace Bing.Utils.Parameters.Parsers
{
    /// <summary>
    /// 参数解析器基类
    /// </summary>
    public abstract class ParameterParserBase : IParameterParser
    {
        /// <summary>
        /// 参数字典
        /// </summary>
        private readonly IDictionary<string, object> _params;

        /// <summary>
        /// 初始化一个<see cref="ParameterParserBase"/>类型的实例
        /// </summary>
        protected ParameterParserBase() => _params = new Dictionary<string, object>();

        /// <summary>
        /// 初始化一个<see cref="ParameterParserBase"/>类型的实例
        /// </summary>
        /// <param name="parser">参数解析器</param>
        protected ParameterParserBase(IParameterParser parser) : this(parser.GetDictionary()) { }

        /// <summary>
        /// 初始化一个<see cref="ParameterParserBase"/>类型的实例
        /// </summary>
        /// <param name="dictionary">字典</param>
        protected ParameterParserBase(IDictionary<string, object> dictionary)
        {
            _params = dictionary == null
                ? new Dictionary<string, object>()
                : new Dictionary<string, object>(dictionary);
        }

        /// <summary>
        /// 添加参数，如果参数已存在则替换
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        protected void Add(string key, object value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;
            value = GetFormatValue(value);
            if (string.IsNullOrWhiteSpace(value.SafeString()))
                return;
            key = key.Trim();
            if (_params.ContainsKey(key))
                _params[key] = value;
            else
                _params.Add(key, value);
        }

        /// <summary>
        /// 获取格式化后的值
        /// </summary>
        /// <param name="value">值</param>
        private string GetFormatValue(object value)
        {
            if (value == null)
                return null;
            if (value is DateTime dateTime)
                return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
            if (value is bool boolValue)
                return boolValue.ToString().ToLower();
            if (value is decimal)
                return value.SafeString();
            return value.SafeString();
        }

        /// <summary>
        /// 获取值
        /// </summary>
        /// <param name="name">参数名</param>
        public object GetValue(string name)
        {
            if (name.IsEmpty())
                return string.Empty;
            return _params.ContainsKey(name) ? _params[name].SafeString() : string.Empty;
        }

        /// <summary>
        /// 获取值
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="name">参数名</param>
        public T GetValue<T>(string name) => Conv.To<T>(GetValue(name));

        /// <summary>
        /// 获取字典
        /// </summary>
        public IDictionary<string, object> GetDictionary() => _params;

        /// <summary>
        /// 是否包含指定键
        /// </summary>
        /// <param name="key">键</param>
        public bool HasKey(string key) => !key.IsEmpty() && _params.ContainsKey(key);

        /// <summary>
        /// 加载数据
        /// </summary>
        /// <param name="data">数据</param>
        public abstract void LoadData(string data);

        /// <summary>
        /// 转换为Json
        /// </summary>
        /// <param name="isConvertToSingleQuotes">是否将双引号转换为单引号</param>
        public string ToJson(bool isConvertToSingleQuotes = false) => JsonHelper.ToJson(_params, isConvertToSingleQuotes);
    }
}

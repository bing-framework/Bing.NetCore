using System;
using System.Collections.Generic;
using System.Linq;
using Bing.Extensions;
using Bing.Utils.Extensions;
using Bing.Utils.Parameters.Parsers;

namespace Bing.Biz.OAuthLogin.Core
{
    /// <summary>
    /// 授权结果
    /// </summary>
    public class AuthorizationResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// 授权接口返回的原始消息
        /// </summary>
        public string Raw { get; }

        /// <summary>
        /// 结果
        /// </summary>
        public string Result => _parser.ToJson();

        /// <summary>
        /// 消息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 请求参数
        /// </summary>
        public string Parameter { get; set; }

        /// <summary>
        /// 参数解析器
        /// </summary>
        private readonly IParameterParser _parser;

        /// <summary>
        /// 初始化一个<see cref="AuthorizationResult"/>类型的实例
        /// </summary>
        /// <param name="raw">授权接口返回的原始消息</param>
        /// <param name="success">是否成功</param>
        /// <param name="type">参数解析器类型</param>
        public AuthorizationResult(string raw, Func<AuthorizationResult, bool> success,
            ParameterParserType type = ParameterParserType.Json)
        {
            Raw = raw;
            IParameterParserFactory factory = new ParameterParserFactory();
            _parser = factory.CreateParameterParser(type);
            _parser.LoadData(raw);
            Success = success(this);
        }

        /// <summary>
        /// 获取字典
        /// </summary>
        /// <returns></returns>
        public IDictionary<string, string> GetDictionary()
        {
            return _parser.GetDictionary().ToDictionary(t => t.Key, t => t.Value.SafeString());
        }

        /// <summary>
        /// 获取值
        /// </summary>
        /// <param name="key">键</param>
        /// <returns></returns>
        public string GetValue(string key)
        {
            return _parser.GetValue(key).SafeString();
        }

        /// <summary>
        /// 是否包含指定键
        /// </summary>
        /// <param name="key">键</param>
        /// <returns></returns>
        public bool HasKey(string key)
        {
            return _parser.HasKey(key);
        }
    }
}

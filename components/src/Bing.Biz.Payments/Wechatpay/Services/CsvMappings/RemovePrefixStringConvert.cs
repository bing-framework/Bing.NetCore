using System;
using TinyCsvParser.TypeConverter;

namespace Bing.Biz.Payments.Wechatpay.Services.CsvMappings
{
    /// <summary>
    /// 移除前缀`字符的字符串列转换器
    /// </summary>
    internal class RemovePrefixStringConvert : ITypeConverter<string>
    {
        /// <summary>
        /// 转换
        /// </summary>
        public bool TryConvert(string value, out string result)
        {
            result = string.IsNullOrWhiteSpace(value) ? "" : value.TrimStart('`');
            return true;
        }

        /// <summary>
        /// 目标类型
        /// </summary>
        public Type TargetType => typeof(string);
    }
}

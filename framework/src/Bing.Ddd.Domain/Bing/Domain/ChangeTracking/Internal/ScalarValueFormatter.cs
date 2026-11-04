using System;
using System.Globalization;

namespace Bing.Domain.ChangeTracking.Internal;

/// <summary>
/// 统一把选中参与变更描述的值格式化为与类型值判等一致的展示文本。
/// </summary>
/// <remarks>
/// 格式化只负责展示；差异判定由调用方使用类型值语义完成，不能反向解析展示文本。
/// </remarks>
internal static class ScalarValueFormatter
{
    /// <summary>
    /// 将值格式化为稳定、文化无关的展示文本。
    /// </summary>
    /// <param name="value">要格式化的值。</param>
    /// <returns>可区分 null、空字符串与普通文本的展示字符串。</returns>
    internal static string Format(object value)
    {
        if (value == null)
            return "null";
        if (value is string text)
            return Quote(text);
        if (value is DateTime dateTime)
            return dateTime.ToString("O", CultureInfo.InvariantCulture);
        if (value is DateTimeOffset offset)
            return offset.ToString("O", CultureInfo.InvariantCulture);
        if (value is IFormattable formattable)
            return formattable.ToString(null, CultureInfo.InvariantCulture);
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    /// <summary>
    /// 为字符串添加引用并转义控制字符。
    /// </summary>
    /// <param name="value">要引用的字符串。</param>
    /// <returns>带双引号且完成转义的字符串。</returns>
    internal static string Quote(string value) => "\"" + (value ?? string.Empty)
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"")
        .Replace("\r", "\\r")
        .Replace("\n", "\\n")
        .Replace("\t", "\\t") + "\"";
}

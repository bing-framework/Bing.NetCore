using System.Buffers;
using System.Text;

namespace Bing.Caching;

/// <summary>
/// 将 Redis 返回的 JSON 片段组装为一个 JSON 数组。
/// </summary>
/// <remarks>
/// Redis 中的值已经是 JSON 片段，因此这里不会再次转义或解析片段。
/// 组装过程只租用临时字符缓冲；返回值是供 JSON 解析器消费的独立字符串。
/// </remarks>
internal static class CacheJsonBatch
{
    /// <summary>
    /// 表示 JSON <c>null</c> 值的文本令牌。
    /// </summary>
    private const string JsonNull = "null";

    /// <summary>
    /// 将 JSON 片段组装为 JSON 数组。
    /// </summary>
    /// <param name="rawValues">Redis 返回的 JSON 片段；null 元素表示缺失值。</param>
    /// <param name="defaultValueJson">缺失值使用的 JSON token；为 null 时使用 JSON null。</param>
    /// <returns>完整的 JSON 数组文本。</returns>
    internal static string Build(string[] rawValues, string defaultValueJson)
    {
        if (rawValues == null)
            throw new ArgumentNullException(nameof(rawValues));

        var replacement = defaultValueJson ?? JsonNull;
        if (rawValues.Length == 0)
            return "[]";

        // 仅计算安全上界；替换判断留在写入阶段，避免为每个片段重复扫描空白和 null token。
        var requiredLength = GetRequiredLength(rawValues, replacement);
        var buffer = ArrayPool<char>.Shared.Rent(requiredLength);
        try
        {
            var offset = 0;
            buffer[offset++] = '[';
            for (var index = 0; index < rawValues.Length; index++)
            {
                if (index > 0)
                    buffer[offset++] = ',';

                offset = AppendStringValue(buffer, offset, rawValues[index], replacement,
                    replaceJsonNull: defaultValueJson != null);
            }
            buffer[offset++] = ']';
            return new string(buffer, 0, offset);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// 将 JSON 片段组装为 JSON 数组。
    /// </summary>
    /// <param name="rawValues">Redis 返回的 UTF-8 JSON 片段；null 元素表示缺失值。</param>
    /// <param name="defaultValueJson">缺失值使用的 JSON token；为 null 时使用 JSON null。</param>
    /// <returns>完整的 JSON 数组文本。</returns>
    internal static string Build(byte[][] rawValues, string defaultValueJson)
    {
        if (rawValues == null)
            throw new ArgumentNullException(nameof(rawValues));

        var replacement = defaultValueJson ?? JsonNull;
        if (rawValues.Length == 0)
            return "[]";

        // GetMaxCharCount 只按输入字节长度计算容量上界，实际 UTF-8 解码只在写入阶段执行一次。
        var requiredLength = GetRequiredLength(rawValues, replacement);
        var buffer = ArrayPool<char>.Shared.Rent(requiredLength);
        try
        {
            var offset = 0;
            buffer[offset++] = '[';
            for (var index = 0; index < rawValues.Length; index++)
            {
                if (index > 0)
                    buffer[offset++] = ',';

                var value = rawValues[index];
                if (value == null)
                {
                    replacement.CopyTo(0, buffer, offset, replacement.Length);
                    offset += replacement.Length;
                    continue;
                }

                var valueOffset = offset;
                var valueLength = Encoding.UTF8.GetChars(value, 0, value.Length, buffer, offset);
                if (defaultValueJson != null &&
                    (IsWhiteSpace(buffer, valueOffset, valueLength) ||
                     IsJsonNullToken(buffer, valueOffset, valueLength)))
                {
                    replacement.CopyTo(0, buffer, valueOffset, replacement.Length);
                    offset = valueOffset + replacement.Length;
                }
                else
                {
                    offset = valueOffset + valueLength;
                }
            }
            buffer[offset++] = ']';
            return new string(buffer, 0, offset);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// 计算 JSON 片段组装所需的缓冲区长度。
    /// </summary>
    /// <param name="rawValues">Redis 返回的字符串 JSON 片段。</param>
    /// <param name="replacement">缺失值替换 token。</param>
    /// <returns>安全的字符缓冲区长度上界。</returns>
    private static int GetRequiredLength(string[] rawValues, string replacement)
    {
        var length = 2;
        checked
        {
            length += rawValues.Length - 1;
            for (var index = 0; index < rawValues.Length; index++)
            {
                var value = rawValues[index];
                // 空白、null token 和 null 元素都可能被替换；两者长度的较大值保证写入不越界。
                length += Math.Max(value?.Length ?? 0, replacement.Length);
            }
        }
        return length;
    }

    /// <summary>
    /// 计算 JSON 片段组装所需的缓冲区长度。
    /// </summary>
    /// <param name="rawValues">Redis 返回的 UTF-8 JSON 片段。</param>
    /// <param name="replacement">缺失值替换 token。</param>
    /// <returns>安全的字符缓冲区长度上界。</returns>
    private static int GetRequiredLength(byte[][] rawValues, string replacement)
    {
        var length = 2;
        checked
        {
            length += rawValues.Length - 1;
            for (var index = 0; index < rawValues.Length; index++)
            {
                var value = rawValues[index];
                // UTF-8 最大字符数是安全上界；实际字符数只在 GetChars 写入时计算。
                var valueLength = value == null ? 0 : Encoding.UTF8.GetMaxCharCount(value.Length);
                length += Math.Max(valueLength, replacement.Length);
            }
        }
        return length;
    }

    /// <summary>
    /// 将一个字符串 JSON 值写入缓冲区。
    /// </summary>
    /// <param name="buffer">目标字符缓冲区。</param>
    /// <param name="offset">写入起始位置。</param>
    /// <param name="value">待写入的 JSON 片段。</param>
    /// <param name="replacement">缺失值替换 token。</param>
    /// <param name="replaceJsonNull">是否替换 JSON null token。</param>
    /// <returns>写入结束位置。</returns>
    private static int AppendStringValue(char[] buffer, int offset, string value,
        string replacement, bool replaceJsonNull)
    {
        if (ShouldReplaceString(value, replaceJsonNull))
            value = replacement;
        value.CopyTo(0, buffer, offset, value.Length);
        return offset + value.Length;
    }

    /// <summary>
    /// 判断字符串 JSON 值是否应替换为默认值。
    /// </summary>
    /// <param name="value">待检查的 JSON 片段。</param>
    /// <param name="replaceJsonNull">是否将 JSON null 视为缺失值。</param>
    /// <returns>应替换时返回 <see langword="true"/>。</returns>
    private static bool ShouldReplaceString(string value, bool replaceJsonNull)
    {
        if (value == null || IsWhiteSpace(value))
            return true;
        return replaceJsonNull && IsJsonNullToken(value);
    }

    /// <summary>
    /// 判断字符序列是否只包含空白字符。
    /// </summary>
    /// <param name="value">待检查的字符串。</param>
    /// <returns>只包含空白字符时返回 <see langword="true"/>。</returns>
    private static bool IsWhiteSpace(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (!char.IsWhiteSpace(value[index]))
                return false;
        }
        return true;
    }

    /// <summary>
    /// 判断字符序列是否只包含空白字符。
    /// </summary>
    /// <param name="value">待检查的字符数组。</param>
    /// <param name="offset">检查起始位置。</param>
    /// <param name="length">检查长度。</param>
    /// <returns>区间只包含空白字符时返回 <see langword="true"/>。</returns>
    private static bool IsWhiteSpace(char[] value, int offset, int length)
    {
        for (var index = 0; index < length; index++)
        {
            if (!char.IsWhiteSpace(value[offset + index]))
                return false;
        }
        return true;
    }

    /// <summary>
    /// 判断字符序列是否为 JSON null token。
    /// </summary>
    /// <param name="value">待检查的字符串。</param>
    /// <returns>是 JSON null token 时返回 <see langword="true"/>。</returns>
    private static bool IsJsonNullToken(string value)
    {
        var start = 0;
        var end = value.Length;
        while (start < end && char.IsWhiteSpace(value[start]))
            start++;
        while (end > start && char.IsWhiteSpace(value[end - 1]))
            end--;
        return end - start == 4 &&
               value[start] == 'n' && value[start + 1] == 'u' &&
               value[start + 2] == 'l' && value[start + 3] == 'l';
    }

    /// <summary>
    /// 判断字符序列是否为 JSON null token。
    /// </summary>
    /// <param name="value">待检查的字符数组。</param>
    /// <param name="offset">检查起始位置。</param>
    /// <param name="length">检查长度。</param>
    /// <returns>是 JSON null token 时返回 <see langword="true"/>。</returns>
    private static bool IsJsonNullToken(char[] value, int offset, int length)
    {
        var start = 0;
        var end = length;
        while (start < end && char.IsWhiteSpace(value[offset + start]))
            start++;
        while (end > start && char.IsWhiteSpace(value[offset + end - 1]))
            end--;
        return end - start == 4 &&
               value[offset + start] == 'n' && value[offset + start + 1] == 'u' &&
               value[offset + start + 2] == 'l' && value[offset + start + 3] == 'l';
    }
}

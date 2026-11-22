using System.Globalization;

namespace Bing.Data.KeyGeneration;

/// <summary>
/// 提供 GUID 与正 Int64 主键的规范字符串转换。
/// </summary>
public static class EntityKeyText
{
    /// <summary>
    /// 将正 Int64 主键转换为不带符号和分隔符的十进制字符串。
    /// </summary>
    /// <param name="key">大于零的主键。</param>
    /// <returns>使用不变区域性格式化的十进制主键。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> 小于或等于零。</exception>
    public static string Format(long key)
    {
        if (key <= 0)
            throw new ArgumentOutOfRangeException(nameof(key), key, "主键必须大于零。");

        return key.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 将非空 GUID 主键转换为标准 D 格式字符串。
    /// </summary>
    /// <param name="key">非空 GUID 主键。</param>
    /// <returns>使用小写十六进制字符的 D 格式 GUID。</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> 为空 GUID。</exception>
    public static string Format(Guid key)
    {
        if (key == Guid.Empty)
            throw new ArgumentException("主键不能是空 GUID。", nameof(key));

        return key.ToString("D");
    }

    /// <summary>
    /// 解析规范的正 Int64 十进制主键字符串。
    /// </summary>
    /// <param name="text">只包含 ASCII 数字且不带前导零的十进制字符串。</param>
    /// <returns>解析得到的正 Int64 主键。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="FormatException">字符串不符合规范或数值超出 Int64 范围。</exception>
    public static long ParseInt64(string text)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));

        if (text.Length == 0 || text[0] == '0')
            throw InvalidInt64(text);

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is < '0' or > '9')
                throw InvalidInt64(text);
        }

        if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var key) || key <= 0)
            throw InvalidInt64(text);

        return key;
    }

    /// <summary>
    /// 解析标准 D 格式的非空 GUID 主键字符串。
    /// </summary>
    /// <param name="text">标准 D 格式 GUID 字符串，十六进制字符大小写均可。</param>
    /// <returns>解析得到的非空 GUID 主键。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="FormatException">字符串不是规范 D 格式 GUID 或表示空 GUID。</exception>
    public static Guid ParseGuid(string text)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));

        if (!Guid.TryParseExact(text, "D", out var key) ||
            key == Guid.Empty ||
            !string.Equals(key.ToString("D"), text, StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException($"'{text}' 不是有效的非空 D 格式 GUID 主键。");
        }

        return key;
    }

    /// <summary>
    /// 创建正 Int64 主键文本的格式异常。
    /// </summary>
    /// <param name="text">不能解析为正 Int64 主键的输入文本。</param>
    /// <returns>包含无效输入文本的格式异常。</returns>
    private static FormatException InvalidInt64(string text) =>
        new($"'{text}' 不是有效的正 Int64 主键字符串。");
}

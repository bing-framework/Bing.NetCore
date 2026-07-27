using System.Text;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 严格结构化标识符路径解析器。
/// </summary>
internal static class SqlIdentifierPathParser
{
    /// <summary>
    /// 解析包含一至三段的结构化标识符路径。
    /// </summary>
    /// <param name="value">待解析的标识符路径。</param>
    /// <param name="path">解析后的标识符路径。</param>
    /// <returns>路径有效时返回 true。</returns>
    public static bool TryParse(string value, out SqlIdentifierPath path)
    {
        path = null;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        value = value.Trim();
        var segments = new List<string>();
        var index = 0;
        while (index < value.Length)
        {
            if (segments.Count == 3 || TryReadSegment(value, ref index, out var segment) == false)
                return false;
            segments.Add(segment);
            if (index == value.Length)
                break;
            if (value[index] != '.')
                return false;
            index++;
            if (index == value.Length)
                return false;
        }

        if (segments.Count == 0)
            return false;
        path = new SqlIdentifierPath(segments);
        return true;
    }

    /// <summary>
    /// 读取单个标识符段。
    /// </summary>
    /// <param name="value">待解析文本。</param>
    /// <param name="index">当前读取索引。</param>
    /// <param name="segment">读取到的逻辑标识符名称。</param>
    /// <returns>读取成功时返回 true。</returns>
    private static bool TryReadSegment(string value, ref int index, out string segment)
    {
        segment = null;
        if (index >= value.Length)
            return false;
        var opening = value[index];
        if (opening is '[' or '"' or '`')
            return TryReadQuotedSegment(value, ref index, opening, opening == '[' ? ']' : opening, out segment);

        var startIndex = index;
        while (index < value.Length && value[index] != '.')
        {
            var current = value[index];
            if (char.IsLetterOrDigit(current) == false && current != '_' && current != '$')
                return false;
            index++;
        }
        if (index == startIndex)
            return false;
        segment = value.Substring(startIndex, index - startIndex);
        return true;
    }

    /// <summary>
    /// 读取带双写转义规则的引用标识符段。
    /// </summary>
    /// <param name="value">待解析文本。</param>
    /// <param name="index">当前读取索引。</param>
    /// <param name="opening">起始引用符。</param>
    /// <param name="closing">结束引用符。</param>
    /// <param name="segment">读取到的逻辑标识符名称。</param>
    /// <returns>读取成功时返回 true。</returns>
    private static bool TryReadQuotedSegment(string value, ref int index, char opening, char closing,
        out string segment)
    {
        segment = null;
        if (value[index] != opening)
            return false;

        var result = new StringBuilder();
        for (index++; index < value.Length; index++)
        {
            var current = value[index];
            if (current != closing)
            {
                if (current is '\r' or '\n' or ';')
                    return false;
                result.Append(current);
                continue;
            }
            if (index + 1 < value.Length && value[index + 1] == closing)
            {
                result.Append(closing);
                index++;
                continue;
            }
            if (result.Length == 0)
                return false;
            index++;
            if (index < value.Length && value[index] != '.')
                return false;
            segment = result.ToString();
            return true;
        }
        return false;
    }
}

/// <summary>
/// 已解析的结构化标识符路径。
/// </summary>
internal sealed class SqlIdentifierPath
{
    /// <summary>
    /// 路径的逻辑标识符段。
    /// </summary>
    private readonly IReadOnlyList<string> _segments;

    /// <summary>
    /// 初始化一个<see cref="SqlIdentifierPath"/>类型的实例。
    /// </summary>
    /// <param name="segments">已解析的逻辑标识符段。</param>
    public SqlIdentifierPath(IReadOnlyList<string> segments) => _segments = segments;

    /// <summary>
    /// 数据库名称；路径少于三段时返回 null。
    /// </summary>
    public string DatabaseName => _segments.Count == 3 ? _segments[0] : null;

    /// <summary>
    /// 表、架构或别名前缀；路径少于两段时返回 null。
    /// </summary>
    public string Prefix => _segments.Count > 1 ? _segments[^2] : null;

    /// <summary>
    /// 列名称。
    /// </summary>
    public string Name => _segments[^1];

    /// <summary>
    /// 用于历史便捷聚合 API 默认 Alias 的叶子列名称。
    /// </summary>
    public string LeafName => Name;
}
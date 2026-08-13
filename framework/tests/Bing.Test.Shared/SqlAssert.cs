using System.Text;
using Bing.Data.Sql.Builders.Params;
using Xunit;

namespace Bing.Test.Shared;

/// <summary>
/// SQL 文本断言。
/// </summary>
public static class SqlAssert
{
    /// <summary>
    /// 断言 SQL 与期望值完全一致，只规范化换行、首尾空白和公共缩进。
    /// </summary>
    /// <param name="expectedSql">测试方法中声明的目标 SQL。</param>
    /// <param name="actualSql">实际 SQL。</param>
    /// <param name="provider">当前 Provider 标识。</param>
    /// <param name="parameters">实际参数，用于失败诊断。</param>
    public static void Equal(string expectedSql, string actualSql, string provider = null,
        IReadOnlyCollection<SqlParam> parameters = null)
    {
        var expected = Normalize(expectedSql);
        var actual = Normalize(actualSql);
        if (string.Equals(expected, actual, StringComparison.Ordinal))
            return;

        var message = new StringBuilder();
        message.AppendLine($"Provider: {provider ?? "<unknown>"}");
        message.AppendLine("Expected SQL:");
        message.AppendLine(expected);
        message.AppendLine("Actual SQL:");
        message.AppendLine(actual);
        if (parameters?.Count > 0)
        {
            message.AppendLine("Parameters:");
            foreach (var parameter in parameters)
                message.AppendLine(SqlParameterAssert.Describe(parameter));
        }
        Assert.Fail(message.ToString());
    }

    /// <summary>
    /// 规范化 SQL 测试文本允许的格式差异。
    /// </summary>
    /// <param name="sql">SQL 文本。</param>
    /// <returns>仅移除公共缩进后的规范文本。</returns>
    private static string Normalize(string sql)
    {
        if (sql == null)
            return null;
        var lines = sql.Replace("\r\n", "\n").Replace('\r', '\n').Trim().Split('\n');
        var contentLines = lines.Where(line => string.IsNullOrWhiteSpace(line) == false).ToArray();
        if (contentLines.Length == 0)
            return string.Empty;
        var commonIndent = contentLines.Min(GetLeadingWhitespaceCount);
        return string.Join("\n", lines.Select(line => line.Length == 0 ? line : line.Substring(Math.Min(commonIndent, line.Length)))).Trim();
    }

    /// <summary>
    /// 获取行首空白字符数量。
    /// </summary>
    private static int GetLeadingWhitespaceCount(string value)
    {
        var index = 0;
        while (index < value.Length && char.IsWhiteSpace(value[index]))
            index++;
        return index;
    }
}
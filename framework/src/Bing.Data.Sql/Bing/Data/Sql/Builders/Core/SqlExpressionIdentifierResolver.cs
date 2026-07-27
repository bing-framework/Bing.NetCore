using System.Text;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 聚合表达式中的方括号标识符解析器。
/// </summary>
internal static class SqlExpressionIdentifierResolver
{
    /// <summary>
    /// 按方言转换普通 SQL 上下文中的方括号标识符，并保留字符串与注释原文。
    /// </summary>
    /// <param name="expressionSql">聚合表达式 SQL。</param>
    /// <param name="dialect">目标 SQL 方言。</param>
    /// <returns>转换后的聚合表达式 SQL。</returns>
    /// <exception cref="ArgumentException">表达式包含未闭合或无效的词法上下文时抛出。</exception>
    public static string Resolve(string expressionSql, IDialect dialect)
    {
        if (expressionSql == null)
            throw new ArgumentNullException(nameof(expressionSql));
        if (dialect == null)
            throw new ArgumentNullException(nameof(dialect));

        var result = new StringBuilder(expressionSql.Length);
        for (var index = 0; index < expressionSql.Length; index++)
        {
            var current = expressionSql[index];
            switch (current)
            {
                case '\'':
                case '"':
                case '`':
                    AppendQuotedText(expressionSql, result, ref index, current);
                    break;
                case '-' when IsNext(expressionSql, index, '-'):
                    AppendLineComment(expressionSql, result, ref index);
                    break;
                case '/' when IsNext(expressionSql, index, '*'):
                    AppendBlockComment(expressionSql, result, ref index);
                    break;
                case '[':
                    result.Append(dialect.SafeName(ReadBracketIdentifier(expressionSql, ref index)));
                    break;
                case ']':
                    throw CreateInvalidExpressionException("存在未匹配的方括号结束符。");
                default:
                    result.Append(current);
                    break;
            }
        }
        return result.ToString();
    }

    /// <summary>
    /// 判断当前位置后是否为指定字符。
    /// </summary>
    /// <param name="value">待检查文本。</param>
    /// <param name="index">当前位置。</param>
    /// <param name="expected">期望的后续字符。</param>
    /// <returns>后续字符匹配时返回 true。</returns>
    private static bool IsNext(string value, int index, char expected) =>
        index + 1 < value.Length && value[index + 1] == expected;

    /// <summary>
    /// 追加带双写转义规则的引用文本。
    /// </summary>
    /// <param name="value">待解析文本。</param>
    /// <param name="result">输出缓冲区。</param>
    /// <param name="index">当前索引。</param>
    /// <param name="quote">引用字符。</param>
    /// <exception cref="ArgumentException">引用文本未闭合时抛出。</exception>
    private static void AppendQuotedText(string value, StringBuilder result, ref int index, char quote)
    {
        result.Append(quote);
        for (index++; index < value.Length; index++)
        {
            var current = value[index];
            result.Append(current);
            if (current != quote)
                continue;
            if (IsNext(value, index, quote))
            {
                result.Append(quote);
                index++;
                continue;
            }
            return;
        }
        throw CreateInvalidExpressionException("存在未闭合的引用文本。");
    }

    /// <summary>
    /// 追加单行注释原文。
    /// </summary>
    /// <param name="value">待解析文本。</param>
    /// <param name="result">输出缓冲区。</param>
    /// <param name="index">当前索引。</param>
    private static void AppendLineComment(string value, StringBuilder result, ref int index)
    {
        result.Append("--");
        index++;
        while (index + 1 < value.Length && value[index + 1] is not '\r' and not '\n')
        {
            index++;
            result.Append(value[index]);
        }
    }

    /// <summary>
    /// 追加块注释原文。
    /// </summary>
    /// <param name="value">待解析文本。</param>
    /// <param name="result">输出缓冲区。</param>
    /// <param name="index">当前索引。</param>
    /// <exception cref="ArgumentException">块注释未闭合时抛出。</exception>
    private static void AppendBlockComment(string value, StringBuilder result, ref int index)
    {
        result.Append("/*");
        index++;
        while (index + 1 < value.Length)
        {
            index++;
            var current = value[index];
            result.Append(current);
            if (current == '*' && IsNext(value, index, '/'))
            {
                index++;
                result.Append('/');
                return;
            }
        }
        throw CreateInvalidExpressionException("存在未闭合的块注释。");
    }

    /// <summary>
    /// 读取方括号标识符并返回逻辑名称。
    /// </summary>
    /// <param name="value">待解析文本。</param>
    /// <param name="index">当前索引。</param>
    /// <returns>去除方括号并还原转义后的逻辑标识符名称。</returns>
    /// <exception cref="ArgumentException">标识符为空、嵌套或未闭合时抛出。</exception>
    private static string ReadBracketIdentifier(string value, ref int index)
    {
        var result = new StringBuilder();
        for (index++; index < value.Length; index++)
        {
            var current = value[index];
            if (current == '[')
                throw CreateInvalidExpressionException("方括号标识符不支持嵌套。");
            if (current != ']')
            {
                result.Append(current);
                continue;
            }
            if (IsNext(value, index, ']'))
            {
                result.Append(']');
                index++;
                continue;
            }
            if (result.Length == 0)
                throw CreateInvalidExpressionException("方括号标识符不能为空。");
            return result.ToString();
        }
        throw CreateInvalidExpressionException("存在未闭合的方括号标识符。");
    }

    /// <summary>
    /// 创建表达式参数异常。
    /// </summary>
    /// <param name="message">异常消息。</param>
    /// <returns>带表达式参数名称的异常。</returns>
    private static ArgumentException CreateInvalidExpressionException(string message) =>
        new($"聚合表达式 {message}", "expressionSql");
}
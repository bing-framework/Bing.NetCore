using System.Text;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// MySQL 字符串表名解析器。
/// </summary>
internal static class MySqlTableNameParser
{
    /// <summary>
    /// 解析 MySQL 字符串表名及别名。
    /// </summary>
    /// <param name="table">表名。</param>
    /// <param name="alias">显式别名。</param>
    /// <returns>已验证的表名、别名和可选架构名。</returns>
    public static MySqlTableName Parse(string table, string alias = null)
    {
        if (string.IsNullOrWhiteSpace(table))
            throw new ArgumentException("表名不能为空。", nameof(table));
        ValidateUnsafeCharacters(table, nameof(table));
        var index = 0;
        var components = ParseComponents(table, ref index);
        SkipWhiteSpace(table, ref index);
        var embeddedAlias = ParseEmbeddedAlias(table, ref index);
        alias = NormalizeIdentifier(alias, nameof(alias));
        ValidateIdentifier(alias, nameof(alias), false);
        ValidateReservedKeyword(alias, nameof(alias));
        ValidateAliasConflict(alias, embeddedAlias);

        if (components.Count > 2 && components.Any(component => component.Quoted))
            throw new ArgumentException("MySQL 完整表名仅支持架构名和表名两个反引号标识符。", nameof(table));
        var hasQuotedSchemaAndTable = components.Count == 2 && components[0].Quoted && components[1].Quoted;
        var schema = hasQuotedSchemaAndTable ? components[0].Value : null;
        var tableName = hasQuotedSchemaAndTable
            ? components[1].Value
            : string.Join(".", components.Select(component => component.Value));
        return new MySqlTableName(tableName, string.IsNullOrWhiteSpace(alias) ? embeddedAlias : alias, schema);
    }

    /// <summary>
    /// 解析由句点分隔的表名段。
    /// </summary>
    private static List<MySqlIdentifierComponent> ParseComponents(string value, ref int index)
    {
        var result = new List<MySqlIdentifierComponent>();
        while (true)
        {
            if (index >= value.Length || char.IsWhiteSpace(value[index]))
                throw new ArgumentException("表名包含空标识符。", nameof(value));
            var component = value[index] == '`'
                ? ParseQuotedComponent(value, ref index)
                : ParseUnquotedComponent(value, ref index);
            ValidateIdentifier(component.Value, nameof(value), true, component.Quoted);
            result.Add(component);
            if (index >= value.Length || char.IsWhiteSpace(value[index]))
                return result;
            if (value[index] != '.')
                throw new ArgumentException("表名仅支持由句点分隔的标识符及单个别名。", nameof(value));
            index++;
            if (index >= value.Length || char.IsWhiteSpace(value[index]) || value[index] == '.')
                throw new ArgumentException("表名包含空标识符。", nameof(value));
        }
    }

    /// <summary>
    /// 解析反引号包围的标识符段。
    /// </summary>
    private static MySqlIdentifierComponent ParseQuotedComponent(string value, ref int index)
    {
        index++;
        var identifier = new StringBuilder();
        var closed = false;
        while (index < value.Length)
        {
            var current = value[index++];
            if (current != '`')
            {
                identifier.Append(current);
                continue;
            }
            if (index < value.Length && value[index] == '`')
            {
                identifier.Append('`');
                index++;
                continue;
            }
            closed = true;
            break;
        }
        if (closed == false)
            throw new ArgumentException("SQL 标识符引号未闭合。", nameof(value));
        if (index < value.Length && char.IsWhiteSpace(value[index]) == false && value[index] != '.')
            throw new ArgumentException("SQL 标识符引号后包含无效字符。", nameof(value));
        return new MySqlIdentifierComponent(identifier.ToString(), true);
    }

    /// <summary>
    /// 解析未加引号的标识符段。
    /// </summary>
    private static MySqlIdentifierComponent ParseUnquotedComponent(string value, ref int index)
    {
        var start = index;
        while (index < value.Length && char.IsWhiteSpace(value[index]) == false && value[index] != '.')
        {
            if (value[index] == '`')
                throw new ArgumentException("SQL 标识符引号位置无效。", nameof(value));
            index++;
        }
        return new MySqlIdentifierComponent(value.Substring(start, index - start), false);
    }

    /// <summary>
    /// 解析字符串内的可选别名。
    /// </summary>
    private static string ParseEmbeddedAlias(string value, ref int index)
    {
        if (index >= value.Length)
            return null;
        var token = ReadToken(value, ref index);
        if (string.Equals(token, "As", StringComparison.OrdinalIgnoreCase))
        {
            SkipWhiteSpace(value, ref index);
            if (index >= value.Length)
                throw new ArgumentException("表别名不能为空。", nameof(value));
            token = ReadToken(value, ref index);
        }
        SkipWhiteSpace(value, ref index);
        if (index != value.Length)
            throw new ArgumentException("表名仅支持由句点分隔的标识符及单个别名。", nameof(value));
        var alias = NormalizeIdentifier(token, nameof(value));
        ValidateIdentifier(alias, nameof(value), false);
        ValidateReservedKeyword(alias, nameof(value));
        return alias;
    }

    /// <summary>
    /// 读取别名标识符。
    /// </summary>
    private static string ReadToken(string value, ref int index)
    {
        if (value[index] == '`')
            return ParseQuotedComponent(value, ref index).Value;
        var start = index;
        while (index < value.Length && char.IsWhiteSpace(value[index]) == false)
            index++;
        return value.Substring(start, index - start);
    }

    /// <summary>
    /// 规范化独立传入的标识符。
    /// </summary>
    private static string NormalizeIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;
        value = value.Trim();
        if (value[0] != '`')
        {
            if (value.IndexOf('`') >= 0)
                throw new ArgumentException("SQL 标识符引号位置无效。", parameterName);
            return value;
        }
        if (value.Length < 2 || value[^1] != '`')
            throw new ArgumentException("SQL 标识符引号未闭合。", parameterName);
        return value.Substring(1, value.Length - 2);
    }

    /// <summary>
    /// 验证标识符。
    /// </summary>
    private static void ValidateIdentifier(string value, string parameterName, bool allowDot, bool allowBacktick = false)
    {
        if (value == null)
            return;
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("SQL 标识符不能为空。", parameterName);
        ValidateUnsafeCharacters(value, parameterName);
        if (value.IndexOfAny(new[] { '[', ']', '"', ',' }) >= 0 ||
            (allowBacktick == false && value.IndexOf('`') >= 0) ||
            (allowDot == false && value.IndexOf('.') >= 0))
            throw new ArgumentException("SQL 标识符包含无效字符。", parameterName);
    }

    /// <summary>
    /// 验证不能作为对象名或别名使用的 SQL 关键字。
    /// </summary>
    private static void ValidateReservedKeyword(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        if (value.Equals("Union", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Join", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Select", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("From", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Where", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("On", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("SQL 标识符不能使用查询关键字。", parameterName);
    }

    /// <summary>
    /// 验证可能改变语义的字符。
    /// </summary>
    private static void ValidateUnsafeCharacters(string value, string parameterName)
    {
        if (value.Any(char.IsControl) || value.IndexOf(';') >= 0 || value.Contains("--") ||
            value.Contains("/*") || value.Contains("*/") || value.IndexOf('(') >= 0 || value.IndexOf(')') >= 0)
            throw new ArgumentException("表名包含无效字符。", parameterName);
    }

    /// <summary>
    /// 跳过空白字符。
    /// </summary>
    private static void SkipWhiteSpace(string value, ref int index)
    {
        while (index < value.Length && char.IsWhiteSpace(value[index]))
            index++;
    }

    /// <summary>
    /// 验证别名冲突。
    /// </summary>
    private static void ValidateAliasConflict(string explicitAlias, string embeddedAlias)
    {
        if (string.IsNullOrWhiteSpace(explicitAlias) || string.IsNullOrWhiteSpace(embeddedAlias))
            return;
        if (string.Equals(explicitAlias, embeddedAlias, StringComparison.OrdinalIgnoreCase) == false)
            throw new InvalidOperationException("字符串表名中的别名与显式别名不一致。");
    }

    /// <summary>
    /// MySQL 标识符段。
    /// </summary>
    private sealed class MySqlIdentifierComponent
    {
        /// <summary>
        /// 初始化标识符段。
        /// </summary>
        public MySqlIdentifierComponent(string value, bool quoted)
        {
            Value = value;
            Quoted = quoted;
        }

        /// <summary>
        /// 标识符值。
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// 是否由反引号包围。
        /// </summary>
        public bool Quoted { get; }
    }
}

/// <summary>
/// MySQL 已验证的字符串表引用组成部分。
/// </summary>
/// <param name="TableName">物理表名。</param>
/// <param name="Alias">可选表别名。</param>
/// <param name="Schema">可选独立架构名。</param>
internal sealed record MySqlTableName(string TableName, string Alias, string Schema);
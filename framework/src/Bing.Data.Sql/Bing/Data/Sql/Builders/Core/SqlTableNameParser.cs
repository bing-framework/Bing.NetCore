namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// SQL 字符串表名解析器。
/// </summary>
internal static class SqlTableNameParser
{
    /// <summary>
    /// 解析安全字符串表名及别名。
    /// </summary>
    /// <param name="table">表名。</param>
    /// <param name="alias">别名。</param>
    /// <param name="schema">独立指定的架构名。</param>
    /// <returns>已验证的原子表名和别名。</returns>
    public static SqlTableName Parse(string table, string alias = null, string schema = null)
    {
        if (string.IsNullOrWhiteSpace(table))
            throw new ArgumentException("表名不能为空。", nameof(table));
        if (table.Any(char.IsControl) || table.IndexOf(';') >= 0 || table.Contains("--") ||
            table.Contains("/*") || table.Contains("*/") || table.IndexOf('(') >= 0 || table.IndexOf(')') >= 0)
            throw new ArgumentException("表名包含无效字符。", nameof(table));
        schema = Unquote(schema, nameof(schema));
        ValidateIdentifier(schema, nameof(schema));

        var tokens = table.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        var name = tokens.Length switch
        {
            1 => tokens[0],
            2 when string.Equals(tokens[1], "as", StringComparison.OrdinalIgnoreCase) == false => tokens[0],
            3 when string.Equals(tokens[1], "as", StringComparison.OrdinalIgnoreCase) => tokens[0],
            _ => throw new ArgumentException("表名仅支持由句点分隔的标识符及单个别名。", nameof(table))
        };
        var embeddedAlias = tokens.Length == 2 ? tokens[1] : tokens.Length == 3 ? tokens[2] : null;
        name = Unquote(name, nameof(table));
        alias = Unquote(alias, nameof(alias));
        embeddedAlias = Unquote(embeddedAlias, nameof(table));
        ValidateIdentifier(name, nameof(table));
        ValidateIdentifier(alias, nameof(alias));
        ValidateIdentifier(embeddedAlias, nameof(table));
        ValidateAliasConflict(alias, embeddedAlias);
        return new SqlTableName(name, string.IsNullOrWhiteSpace(alias) ? embeddedAlias : alias, schema);
    }

    /// <summary>
    /// 验证别名冲突。
    /// </summary>
    /// <param name="explicitAlias">显式别名。</param>
    /// <param name="embeddedAlias">字符串内别名。</param>
    private static void ValidateAliasConflict(string explicitAlias, string embeddedAlias)
    {
        if (string.IsNullOrWhiteSpace(explicitAlias) || string.IsNullOrWhiteSpace(embeddedAlias))
            return;
        if (string.Equals(explicitAlias, embeddedAlias, StringComparison.OrdinalIgnoreCase) == false)
            throw new InvalidOperationException("字符串表名中的别名与显式别名不一致。");
    }

    /// <summary>
    /// 去除完整标识符的成对引号。
    /// </summary>
    private static string Unquote(string identifier, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return identifier;
        identifier = identifier.Trim();
        if (identifier.Length < 2)
            return identifier;
        var first = identifier[0];
        var last = identifier[identifier.Length - 1];
        if ((first == '[' && last == ']') || (first == '`' && last == '`') || (first == '"' && last == '"'))
            return identifier.Substring(1, identifier.Length - 2);
        if (first is '[' or '`' or '"' || last is ']' or '`' or '"')
            throw new ArgumentException("SQL 标识符引号未闭合。", parameterName);
        return identifier;
    }

    /// <summary>
    /// 验证单个动态标识符。
    /// </summary>
    private static void ValidateIdentifier(string identifier, string parameterName)
    {
        if (identifier == null)
            return;
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException("SQL 标识符不能为空。", parameterName);
        if (identifier.Any(char.IsControl) || identifier.IndexOf(';') >= 0 || identifier.Contains("--") ||
            identifier.Contains("/*") || identifier.Contains("*/") || identifier.IndexOf('(') >= 0 ||
            identifier.IndexOf(')') >= 0 || identifier.IndexOf(',') >= 0 || identifier.IndexOf('[') >= 0 ||
            identifier.IndexOf(']') >= 0 || identifier.IndexOf('`') >= 0 || identifier.IndexOf('"') >= 0)
            throw new ArgumentException("SQL 标识符包含无效字符。", parameterName);
    }
}

/// <summary>
/// 已验证的字符串表引用组成部分。
/// </summary>
/// <param name="TableName">完整物理表名。</param>
/// <param name="Alias">可选表别名。</param>
/// <param name="Schema">可选独立架构名。</param>
internal sealed record SqlTableName(string TableName, string Alias, string Schema);
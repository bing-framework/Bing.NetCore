using Bing.Data.Enums;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// SQL 字符串表名解析器。
/// </summary>
internal static class SqlTableNameParser
{
    /// <summary>
    /// 验证字符串表名及别名。
    /// </summary>
    /// <param name="table">表名。</param>
    /// <param name="alias">别名。</param>
    /// <param name="databaseType">数据库类型。</param>
    public static void Validate(string table, string alias, DatabaseType? databaseType)
    {
        if (string.IsNullOrWhiteSpace(table))
            throw new ArgumentException("表名不能为空。", nameof(table));
        if (table.Any(char.IsControl) || table.IndexOf(';') >= 0)
            throw new ArgumentException("表名包含无效字符。", nameof(table));
        ValidateIdentifier(alias, nameof(alias));

        var tokens = table.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        var name = tokens.Length switch
        {
            1 => tokens[0],
            2 when string.Equals(tokens[1], "as", StringComparison.OrdinalIgnoreCase) == false => tokens[0],
            3 when string.Equals(tokens[1], "as", StringComparison.OrdinalIgnoreCase) => tokens[0],
            _ => throw new ArgumentException("表名仅支持由句点分隔的标识符及单个别名。", nameof(table))
        };
        var embeddedAlias = tokens.Length == 2 ? tokens[1] : tokens.Length == 3 ? tokens[2] : null;
        ValidateIdentifier(embeddedAlias, nameof(table));

        var parts = name.Split('.');
        var maximumParts = GetMaximumNameParts(databaseType);
        if (parts.Length > maximumParts)
            throw new InvalidOperationException("SQL 对象名称段数超过当前数据库 Provider 支持的上限。");
        foreach (var part in parts)
            ValidateIdentifier(Unquote(part), nameof(table));
    }

    /// <summary>
    /// 获取 Provider 支持的最大表名段数。
    /// </summary>
    private static int GetMaximumNameParts(DatabaseType? databaseType)
    {
        if (databaseType == DatabaseType.Sqlite)
            return 2;
        if (databaseType == null)
            return 3;
        return new DefaultSqlObjectNameCapabilityProvider().GetCapabilities(databaseType).MaximumNameParts;
    }

    /// <summary>
    /// 去除成对标识符引号。
    /// </summary>
    private static string Unquote(string identifier)
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
        if (identifier.Any(char.IsControl) || identifier.IndexOf(';') >= 0)
            throw new ArgumentException("SQL 标识符包含无效字符。", parameterName);
    }
}
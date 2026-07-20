using Bing.Data.Enums;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 默认数据库方言适配器
/// </summary>
public sealed class DefaultDatabaseDialectAdapter : IDatabaseDialectAdapter
{
    /// <summary>
    /// 获取数据库语法
    /// </summary>
    /// <param name="databaseType">数据库类型</param>
    public CrossDatabaseSyntax GetSyntax(DatabaseType? databaseType) => databaseType switch
    {
        DatabaseType.MySql => CrossDatabaseSyntax.MySql,
        DatabaseType.PgSql => CrossDatabaseSyntax.PostgreSql,
        DatabaseType.Oracle => CrossDatabaseSyntax.Oracle,
        DatabaseType.Sqlite => CrossDatabaseSyntax.Sqlite,
        DatabaseType.Doris => CrossDatabaseSyntax.Doris,
        _ => CrossDatabaseSyntax.SqlServer
    };

    /// <summary>
    /// 格式化表名
    /// </summary>
    /// <param name="table">表标识符</param>
    /// <param name="databaseType">数据库类型</param>
    public string FormatTable(TableIdentifier table, DatabaseType? databaseType)
    {
        var syntax = GetSyntax(databaseType);
        var tableName = Quote(table.Name, syntax);
        if (table.HasSchema == false || syntax.SupportsSchema == false)
            return tableName;
        return $"{Quote(table.Schema, syntax)}.{tableName}";
    }

    /// <summary>
    /// 格式化列名
    /// </summary>
    /// <param name="column">列标识符</param>
    /// <param name="databaseType">数据库类型</param>
    public string FormatColumn(ColumnIdentifier column, DatabaseType? databaseType) =>
        Quote(column.Name, GetSyntax(databaseType));

    /// <summary>
    /// 转义单个标识符
    /// </summary>
    private static string Quote(string identifier, CrossDatabaseSyntax syntax)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException("标识符不能为空。", nameof(identifier));
        if (identifier.IndexOfAny(new[] { '\r', '\n', ';' }) >= 0)
            throw new ArgumentException("标识符包含无效字符。", nameof(identifier));
        var escaped = identifier.Replace(syntax.ClosingIdentifier.ToString(),
            new string(syntax.ClosingIdentifier, 2));
        return $"{syntax.OpeningIdentifier}{escaped}{syntax.ClosingIdentifier}";
    }
}
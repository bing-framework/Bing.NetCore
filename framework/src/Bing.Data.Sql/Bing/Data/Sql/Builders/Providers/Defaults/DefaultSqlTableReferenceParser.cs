namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 默认 SQL 表引用解析器。
/// </summary>
public sealed class DefaultSqlTableReferenceParser : ISqlTableReferenceParser
{
    /// <summary>
    /// 默认实例。
    /// </summary>
    public static DefaultSqlTableReferenceParser Instance { get; } = new();

    /// <inheritdoc />
    public SqlTableName Parse(string table, string alias = null, string schema = null) =>
        SqlTableNameParser.Parse(table, alias, schema);
}
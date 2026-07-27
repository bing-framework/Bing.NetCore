using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Clauses;

/// <summary>
/// Sqlite From子句
/// </summary>
public class SqliteFromClause : FromClause
{
    /// <inheritdoc />
    public SqliteFromClause(SqlClauseContext context)
        : this(context, null, Bing.Data.Enums.DatabaseType.Sqlite)
    {
    }

    /// <summary>
    /// 使用运行上下文初始化 SQLite From 子句。
    /// </summary>
    /// <param name="context">子句运行上下文。</param>
    /// <param name="table">表。</param>
    /// <param name="providerDatabaseType">固定数据库类型。</param>
    protected SqliteFromClause(SqlClauseContext context, SqlItem table,
        Bing.Data.Enums.DatabaseType? providerDatabaseType = null)
        : base(context, table, providerDatabaseType ?? Bing.Data.Enums.DatabaseType.Sqlite)
    {
    }

    /// <inheritdoc />
    protected override SqlItem CreateSqlItem(string table, string schema, string alias) =>
        SqlItem.Parse(table, schema, alias);

    /// <inheritdoc />
    protected override FromClause CreateClone(SqlClauseContext context, SqlItem table) =>
        new SqliteFromClause(context, table, ProviderDatabaseType);
}

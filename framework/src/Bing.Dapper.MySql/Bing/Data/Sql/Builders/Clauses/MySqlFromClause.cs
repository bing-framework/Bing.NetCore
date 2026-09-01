using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Clauses;

/// <summary>
/// MySql From子句
/// </summary>
public class MySqlFromClause : FromClause
{
    /// <summary>
    /// 初始化一个<see cref="MySqlFromClause"/>类型的实例
    /// </summary>
    /// <param name="context">子句运行上下文。</param>
    public MySqlFromClause(SqlClauseContext context)
        : this(context, null, context?.ExecutionContext.DatabaseType ?? Bing.Data.Enums.DatabaseType.MySql)
    {
    }

    /// <summary>
    /// 使用运行上下文初始化 MySQL From 子句。
    /// </summary>
    /// <param name="context">子句运行上下文。</param>
    /// <param name="table">表。</param>
    /// <param name="providerDatabaseType">固定数据库类型。</param>
    protected MySqlFromClause(SqlClauseContext context, SqlItem table,
        Bing.Data.Enums.DatabaseType? providerDatabaseType = null)
        : base(context, table, providerDatabaseType ?? Bing.Data.Enums.DatabaseType.MySql)
    {
    }

    /// <summary>
    /// 创建Sql项
    /// </summary>
    /// <param name="table">表名</param>
    /// <param name="schema">架构名</param>
    /// <param name="alias">别名</param>
    /// <returns>根据 Provider 类型创建的 SQL 项。</returns>
    protected override SqlItem CreateSqlItem(string table, string schema, string alias) =>
        ProviderDatabaseType != Bing.Data.Enums.DatabaseType.MySql
            ? SqlItem.Parse(table, schema, alias)
            : SqlItem.Atomic(table, schema, alias);

    /// <summary>
    /// 解析实际 MySQL 的反引号字符串表名。
    /// </summary>
    /// <param name="table">表名。</param>
    /// <param name="alias">别名。</param>
    /// <returns>表名、别名和架构名。</returns>
    protected override (string TableName, string Alias, string Schema) ParseTableName(string table, string alias)
    {
        if (ProviderDatabaseType != Bing.Data.Enums.DatabaseType.MySql)
            return base.ParseTableName(table, alias);
        var parsedTable = MySqlTableNameParser.Parse(table, alias);
        return (parsedTable.TableName, parsedTable.Alias, parsedTable.Schema);
    }

    /// <inheritdoc />
    protected override FromClause CreateClone(SqlClauseContext context, SqlItem table) =>
        new MySqlFromClause(context, table, ProviderDatabaseType);
}

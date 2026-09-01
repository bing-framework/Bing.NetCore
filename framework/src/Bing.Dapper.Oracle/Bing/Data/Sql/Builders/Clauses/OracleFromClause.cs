using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Clauses;

/// <summary>
/// Oracle From子句
/// </summary>
public class OracleFromClause : FromClause
{
    /// <inheritdoc />
    public OracleFromClause(SqlClauseContext context)
        : this(context, null, Bing.Data.Enums.DatabaseType.Oracle)
    {
    }

    /// <summary>
    /// 使用运行上下文初始化 Oracle From 子句。
    /// </summary>
    /// <param name="context">子句运行上下文。</param>
    /// <param name="table">表。</param>
    /// <param name="providerDatabaseType">固定数据库类型。</param>
    protected OracleFromClause(SqlClauseContext context, SqlItem table,
        Bing.Data.Enums.DatabaseType? providerDatabaseType = null)
        : base(context, table, providerDatabaseType ?? Bing.Data.Enums.DatabaseType.Oracle)
    {
    }

    /// <inheritdoc />
    protected override FromClause CreateClone(SqlClauseContext context, SqlItem table) =>
        new OracleFromClause(context, table, ProviderDatabaseType);

    /// <inheritdoc />
    protected override string GetSubqueryAlias(string alias) => $" {Dialect.SafeName(alias)}";
}

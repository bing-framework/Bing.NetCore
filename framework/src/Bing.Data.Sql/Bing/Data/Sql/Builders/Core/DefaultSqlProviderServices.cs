using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 默认 SQL 子句工厂。
/// </summary>
public sealed class DefaultSqlClauseFactory : ISqlClauseFactory
{
    /// <inheritdoc />
    public ISelectClause CreateSelect(SqlClauseContext context) => new SelectClause(context);

    /// <inheritdoc />
    public IFromClause CreateFrom(SqlClauseContext context) => new FromClause(context);

    /// <inheritdoc />
    public IJoinClause CreateJoin(SqlClauseContext context) => new JoinClause(context);

    /// <inheritdoc />
    public IWhereClause CreateWhere(SqlClauseContext context) => new WhereClause(context);

    /// <inheritdoc />
    public IGroupByClause CreateGroupBy(SqlClauseContext context) => new GroupByClause(context);

    /// <inheritdoc />
    public IOrderByClause CreateOrderBy(SqlClauseContext context) => new OrderByClause(context);
}

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

/// <summary>
/// 默认参数管理器工厂。
/// </summary>
public sealed class DefaultParameterManagerFactory : IParameterManagerFactory
{
    /// <summary>
    /// 默认实例。
    /// </summary>
    public static DefaultParameterManagerFactory Instance { get; } = new();

    /// <inheritdoc />
    public IParameterManager Create(IDialect dialect) => new ParameterManager(dialect);
}
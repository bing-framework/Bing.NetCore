using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// SQL 数据库提供程序。
/// </summary>
/// <remarks>
/// 提供程序及其公开属性必须不可变并可在线程间安全共享。
/// </remarks>
public interface ISqlProvider
{
    /// <summary>
    /// 数据库类型。
    /// </summary>
    DatabaseType DatabaseType { get; }

    /// <summary>
    /// SQL 方言。
    /// </summary>
    IDialect Dialect { get; }

    /// <summary>
    /// SQL 子句工厂。
    /// </summary>
    ISqlClauseFactory ClauseFactory { get; }

    /// <summary>
    /// 表引用解析器。
    /// </summary>
    ISqlTableReferenceParser TableReferenceParser { get; }

    /// <summary>
    /// 分页 SQL 渲染器。
    /// </summary>
    ISqlPaginationRenderer PaginationRenderer { get; }

    /// <summary>
    /// 参数管理器工厂。
    /// </summary>
    IParameterManagerFactory ParameterManagerFactory { get; }

    /// <summary>
    /// 参数字面值解析器。
    /// </summary>
    IParamLiteralsResolver ParamLiteralsResolver { get; }
}

/// <summary>
/// SQL 子句工厂。
/// </summary>
public interface ISqlClauseFactory
{
    /// <summary>
    /// 创建 Select 子句。
    /// </summary>
    /// <param name="context">子句运行上下文。</param>
    /// <returns>Select 子句。</returns>
    ISelectClause CreateSelect(SqlClauseContext context);

    /// <summary>
    /// 创建 From 子句。
    /// </summary>
    /// <param name="context">子句运行上下文。</param>
    /// <returns>From 子句。</returns>
    IFromClause CreateFrom(SqlClauseContext context);

    /// <summary>
    /// 创建 Join 子句。
    /// </summary>
    /// <param name="context">子句运行上下文。</param>
    /// <returns>Join 子句。</returns>
    IJoinClause CreateJoin(SqlClauseContext context);

    /// <summary>
    /// 创建 Where 子句。
    /// </summary>
    /// <param name="context">子句运行上下文。</param>
    /// <returns>Where 子句。</returns>
    IWhereClause CreateWhere(SqlClauseContext context);

    /// <summary>
    /// 创建 Group By 子句。
    /// </summary>
    /// <param name="context">子句运行上下文。</param>
    /// <returns>Group By 子句。</returns>
    IGroupByClause CreateGroupBy(SqlClauseContext context);

    /// <summary>
    /// 创建 Order By 子句。
    /// </summary>
    /// <param name="context">子句运行上下文。</param>
    /// <returns>Order By 子句。</returns>
    IOrderByClause CreateOrderBy(SqlClauseContext context);
}

/// <summary>
/// SQL 字符串表引用解析器。
/// </summary>
public interface ISqlTableReferenceParser
{
    /// <summary>
    /// 解析表名、别名和可选架构。
    /// </summary>
    /// <param name="table">表名。</param>
    /// <param name="alias">显式别名。</param>
    /// <param name="schema">显式架构名。</param>
    /// <returns>已解析的表引用名称。</returns>
    SqlTableName Parse(string table, string alias = null, string schema = null);
}

/// <summary>
/// SQL 分页渲染器。
/// </summary>
public interface ISqlPaginationRenderer
{
    /// <summary>
    /// 渲染分页 SQL 片段。
    /// </summary>
    /// <param name="offsetParameterName">偏移量参数名。</param>
    /// <param name="limitParameterName">限制行数参数名。</param>
    /// <returns>分页 SQL 片段。</returns>
    string Render(string offsetParameterName, string limitParameterName);
}

/// <summary>
/// 参数管理器工厂。
/// </summary>
public interface IParameterManagerFactory
{
    /// <summary>
    /// 创建参数管理器。
    /// </summary>
    /// <param name="dialect">SQL 方言。</param>
    /// <returns>新的参数管理器。</returns>
    IParameterManager Create(IDialect dialect);
}

/// <summary>
/// SQL 参数数量上限提供程序。
/// </summary>
public interface ISqlParameterLimitProvider
{
    /// <summary>
    /// 最大参数数量；未限制时返回 <see langword="null"/>。
    /// </summary>
    int? MaxParameterCount { get; }
}

/// <summary>
/// SQL Builder 工厂。
/// </summary>
public interface ISqlBuilderFactory
{
    /// <summary>
    /// 根据 SQL 提供程序创建 Builder。
    /// </summary>
    /// <param name="provider">SQL 提供程序。</param>
    /// <returns>SQL Builder。</returns>
    ISqlBuilder Create(ISqlProvider provider);

    /// <summary>
    /// 根据数据库类型创建 Builder。
    /// </summary>
    /// <param name="databaseType">数据库类型。</param>
    /// <returns>SQL Builder。</returns>
    ISqlBuilder Create(DatabaseType databaseType);
}
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// Sql组件访问器
/// </summary>
public interface ISqlPartAccessor
{
    /// <summary>
    /// Sql方言
    /// </summary>
    IDialect Dialect { get; }

    /// <summary>
    /// 参数管理器
    /// </summary>
    IParameterManager ParameterManager { get; }

    /// <summary>
    /// Select子句
    /// </summary>
    ISelectClause SelectClause { get; }

    /// <summary>
    /// From子句
    /// </summary>
    IFromClause FromClause { get; }

    /// <summary>
    /// Join子句
    /// </summary>
    IJoinClause JoinClause { get; }

    /// <summary>
    /// Where子句
    /// </summary>
    IWhereClause WhereClause { get; }

    /// <summary>
    /// GroupBy子句
    /// </summary>
    IGroupByClause GroupByClause { get; }

    /// <summary>
    /// OrderBy子句
    /// </summary>
    IOrderByClause OrderByClause { get; }
}

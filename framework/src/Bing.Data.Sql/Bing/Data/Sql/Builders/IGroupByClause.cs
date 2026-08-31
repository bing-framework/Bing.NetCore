using System.Linq.Expressions;
using Bing.Data.Sql.Builders.Clauses;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// 分组子句
/// </summary>
public interface IGroupByClause : ISqlClause, ISqlClauseCloneable<IGroupByClause>
{
    /// <summary>
    /// 是否存在分组
    /// </summary>
    bool IsGroup { get; }

    /// <summary>
    /// 分组列表
    /// </summary>
    string GroupColumns { get; }

    /// <summary>
    /// 分组
    /// </summary>
    /// <param name="groupBy">分组列表</param>
    void GroupBy(string groupBy);

    /// <summary>
    /// 分组
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="columns">分组字段</param>
    void GroupBy<TEntity>(params Expression<Func<TEntity, object>>[] columns);

    /// <summary>
    /// 分组
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="column">分组字段</param>
    void GroupBy<TEntity>(Expression<Func<TEntity, object>> column);

    /// <summary>
    /// 设置 Having 条件，并按当前方言解析方括号标识符。
    /// </summary>
    /// <param name="sql">Having SQL 条件；外部输入必须通过参数 API 提供。</param>
    void Having(string sql);

    /// <summary>
    /// 设置受信任的原始 Having 条件。
    /// </summary>
    /// <param name="sql">Having SQL 条件；调用方负责参数化外部输入。</param>
    void HavingRaw(string sql);

    /// <summary>
    /// 添加到GroupBy子句
    /// </summary>
    /// <param name="sql">Sql语句</param>
    void AppendSql(string sql);

    /// <summary>
    /// 获取Sql
    /// </summary>
    /// <returns>当前 Group By 子句生成的 SQL。</returns>
    string ToSql();
}
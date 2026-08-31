using System.Linq.Expressions;
using Bing.Data.Sql.Builders.Clauses;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// 定义 SQL 排序子句的添加、校验和渲染契约。
/// </summary>
public interface IOrderByClause : ISqlClause, ISqlClauseCloneable<IOrderByClause>
{

    /// <summary>
    /// 添加原始排序列表。
    /// </summary>
    /// <param name="order">排序列表。</param>
    /// <param name="tableAlias">可选表别名。</param>
    void OrderBy(string order, string tableAlias = null);

    /// <summary>
    /// 添加实体列排序。
    /// </summary>
    /// <typeparam name="TEntity">排序实体类型。</typeparam>
    /// <param name="column">排序列表达式。</param>
    /// <param name="desc">是否按降序排列。</param>
    void OrderBy<TEntity>(Expression<Func<TEntity, object>> column, bool desc = false);

    /// <summary>
    /// 向排序子句追加原始 SQL 排序文本。
    /// </summary>
    /// <param name="order">原始排序文本。</param>
    void AppendSql(string order);

    /// <summary>
    /// 验证分页场景下排序子句是否满足要求。
    /// </summary>
    /// <param name="isPage">是否处于分页查询。</param>
    void Validate(bool isPage);

    /// <summary>
    /// 获取当前排序子句生成的 SQL。
    /// </summary>
    /// <returns>当前排序子句生成的 SQL。</returns>
    string ToSql();
}
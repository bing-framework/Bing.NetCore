using System.Linq.Expressions;

namespace Bing.Data.Sql;

/// <summary>
/// 结构化 SQL 条件组。
/// </summary>
public interface ISqlConditionGroup
{
    /// <summary>
    /// 追加单来源 And 条件。
    /// </summary>
    void And<TEntity>(Expression<Func<TEntity, bool>> predicate) where TEntity : class;

    /// <summary>
    /// 按来源别名追加单来源 And 条件。
    /// </summary>
    void And<TEntity>(Expression<Func<TEntity, bool>> predicate, string alias) where TEntity : class;

    /// <summary>
    /// 追加双来源 And 条件。
    /// </summary>
    void And<TFirst, TSecond>(Expression<Func<TFirst, TSecond, bool>> predicate)
        where TFirst : class where TSecond : class;

    /// <summary>
    /// 按来源别名追加双来源 And 条件。
    /// </summary>
    void And<TFirst, TSecond>(Expression<Func<TFirst, TSecond, bool>> predicate,
        string firstAlias, string secondAlias)
        where TFirst : class where TSecond : class;

    /// <summary>
    /// 追加单来源 Or 条件。
    /// </summary>
    void Or<TEntity>(Expression<Func<TEntity, bool>> predicate) where TEntity : class;

    /// <summary>
    /// 按来源别名追加单来源 Or 条件。
    /// </summary>
    void Or<TEntity>(Expression<Func<TEntity, bool>> predicate, string alias) where TEntity : class;

    /// <summary>
    /// 追加双来源 Or 条件。
    /// </summary>
    void Or<TFirst, TSecond>(Expression<Func<TFirst, TSecond, bool>> predicate)
        where TFirst : class where TSecond : class;

    /// <summary>
    /// 按来源别名追加双来源 Or 条件。
    /// </summary>
    void Or<TFirst, TSecond>(Expression<Func<TFirst, TSecond, bool>> predicate,
        string firstAlias, string secondAlias)
        where TFirst : class where TSecond : class;

    /// <summary>
    /// 追加嵌套条件组。
    /// </summary>
    void Group(Action<ISqlConditionGroup> configure);

    /// <summary>按 And 追加嵌套条件组。</summary>
    void AndGroup(Action<ISqlConditionGroup> configure);

    /// <summary>按 Or 追加嵌套条件组。</summary>
    void OrGroup(Action<ISqlConditionGroup> configure);
}

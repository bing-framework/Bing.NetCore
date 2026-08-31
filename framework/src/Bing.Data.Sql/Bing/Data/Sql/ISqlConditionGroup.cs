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
    /// <typeparam name="TEntity">条件所属的实体类型。</typeparam>
    /// <param name="predicate">单来源条件表达式。</param>
    void And<TEntity>(Expression<Func<TEntity, bool>> predicate) where TEntity : class;

    /// <summary>
    /// 按来源别名追加单来源 And 条件。
    /// </summary>
    /// <typeparam name="TEntity">条件所属的实体类型。</typeparam>
    /// <param name="predicate">单来源条件表达式。</param>
    /// <param name="alias">实体来源别名。</param>
    void And<TEntity>(Expression<Func<TEntity, bool>> predicate, string alias) where TEntity : class;

    /// <summary>
    /// 追加双来源 And 条件。
    /// </summary>
    /// <typeparam name="TFirst">第一个条件来源的实体类型。</typeparam>
    /// <typeparam name="TSecond">第二个条件来源的实体类型。</typeparam>
    /// <param name="predicate">双来源条件表达式。</param>
    void And<TFirst, TSecond>(Expression<Func<TFirst, TSecond, bool>> predicate)
        where TFirst : class where TSecond : class;

    /// <summary>
    /// 按来源别名追加双来源 And 条件。
    /// </summary>
    /// <typeparam name="TFirst">第一个条件来源的实体类型。</typeparam>
    /// <typeparam name="TSecond">第二个条件来源的实体类型。</typeparam>
    /// <param name="predicate">双来源条件表达式。</param>
    /// <param name="firstAlias">第一个实体来源别名。</param>
    /// <param name="secondAlias">第二个实体来源别名。</param>
    void And<TFirst, TSecond>(Expression<Func<TFirst, TSecond, bool>> predicate,
        string firstAlias, string secondAlias)
        where TFirst : class where TSecond : class;

    /// <summary>
    /// 追加单来源 Or 条件。
    /// </summary>
    /// <typeparam name="TEntity">条件所属的实体类型。</typeparam>
    /// <param name="predicate">单来源条件表达式。</param>
    void Or<TEntity>(Expression<Func<TEntity, bool>> predicate) where TEntity : class;

    /// <summary>
    /// 按来源别名追加单来源 Or 条件。
    /// </summary>
    /// <typeparam name="TEntity">条件所属的实体类型。</typeparam>
    /// <param name="predicate">单来源条件表达式。</param>
    /// <param name="alias">实体来源别名。</param>
    void Or<TEntity>(Expression<Func<TEntity, bool>> predicate, string alias) where TEntity : class;

    /// <summary>
    /// 追加双来源 Or 条件。
    /// </summary>
    /// <typeparam name="TFirst">第一个条件来源的实体类型。</typeparam>
    /// <typeparam name="TSecond">第二个条件来源的实体类型。</typeparam>
    /// <param name="predicate">双来源条件表达式。</param>
    void Or<TFirst, TSecond>(Expression<Func<TFirst, TSecond, bool>> predicate)
        where TFirst : class where TSecond : class;

    /// <summary>
    /// 按来源别名追加双来源 Or 条件。
    /// </summary>
    /// <typeparam name="TFirst">第一个条件来源的实体类型。</typeparam>
    /// <typeparam name="TSecond">第二个条件来源的实体类型。</typeparam>
    /// <param name="predicate">双来源条件表达式。</param>
    /// <param name="firstAlias">第一个实体来源别名。</param>
    /// <param name="secondAlias">第二个实体来源别名。</param>
    void Or<TFirst, TSecond>(Expression<Func<TFirst, TSecond, bool>> predicate,
        string firstAlias, string secondAlias)
        where TFirst : class where TSecond : class;

    /// <summary>按 And 追加嵌套条件组。</summary>
    /// <param name="configure">配置嵌套条件组的委托。</param>
    void AndGroup(Action<ISqlConditionGroup> configure);

    /// <summary>按 Or 追加嵌套条件组。</summary>
    /// <param name="configure">配置嵌套条件组的委托。</param>
    void OrGroup(Action<ISqlConditionGroup> configure);
}

using System.Linq.Expressions;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql;

/// <summary>
/// 内部结构化 SQL 条件组实现。
/// </summary>
internal sealed class SqlConditionGroup : ISqlConditionGroup
{
    /// <summary>
    /// 将 Lambda 条件解析为结构化条件的委托。
    /// </summary>
    private readonly Func<LambdaExpression, IReadOnlyList<string>, ICondition> _resolver;

    /// <summary>
    /// 当前条件组的根条件。
    /// </summary>
    private ICondition _condition;

    /// <summary>
    /// 初始化一个 <see cref="SqlConditionGroup"/> 类型的实例。
    /// </summary>
    /// <param name="resolver">将 Lambda 表达式解析为条件的委托。</param>
    internal SqlConditionGroup(Func<LambdaExpression, ICondition> resolver)
    {
        if (resolver == null)
            throw new ArgumentNullException(nameof(resolver));
        _resolver = (expression, _) => resolver(expression);
    }

    /// <summary>
    /// 初始化一个 <see cref="SqlConditionGroup"/> 类型的实例。
    /// </summary>
    /// <param name="resolver">将 Lambda 表达式及来源别名解析为条件的委托。</param>
    internal SqlConditionGroup(Func<LambdaExpression, IReadOnlyList<string>, ICondition> resolver)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    /// <summary>
    /// 获取当前条件组的根条件。
    /// </summary>
    internal ICondition Condition => _condition;

    /// <inheritdoc />
    public void And<TEntity>(Expression<Func<TEntity, bool>> predicate) where TEntity : class =>
        Add(predicate, or: false);

    /// <inheritdoc />
    public void And<TEntity>(Expression<Func<TEntity, bool>> predicate, string alias) where TEntity : class =>
        Add(predicate, or: false, new[] { alias });

    /// <inheritdoc />
    public void And<TFirst, TSecond>(Expression<Func<TFirst, TSecond, bool>> predicate)
        where TFirst : class where TSecond : class => Add(predicate, or: false);

    /// <inheritdoc />
    public void And<TFirst, TSecond>(Expression<Func<TFirst, TSecond, bool>> predicate,
        string firstAlias, string secondAlias)
        where TFirst : class where TSecond : class => Add(predicate, or: false, new[] { firstAlias, secondAlias });

    /// <inheritdoc />
    public void Or<TEntity>(Expression<Func<TEntity, bool>> predicate) where TEntity : class =>
        Add(predicate, or: true);

    /// <inheritdoc />
    public void Or<TEntity>(Expression<Func<TEntity, bool>> predicate, string alias) where TEntity : class =>
        Add(predicate, or: true, new[] { alias });

    /// <inheritdoc />
    public void Or<TFirst, TSecond>(Expression<Func<TFirst, TSecond, bool>> predicate)
        where TFirst : class where TSecond : class => Add(predicate, or: true);

    /// <inheritdoc />
    public void Or<TFirst, TSecond>(Expression<Func<TFirst, TSecond, bool>> predicate,
        string firstAlias, string secondAlias)
        where TFirst : class where TSecond : class => Add(predicate, or: true, new[] { firstAlias, secondAlias });

    /// <inheritdoc />
    public void AndGroup(Action<ISqlConditionGroup> configure)
    {
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));
        var nested = new SqlConditionGroup(_resolver);
        configure(nested);
        if (nested.Condition == null)
            return;
        Add(nested.Condition, or: false);
    }

    /// <inheritdoc />
    public void OrGroup(Action<ISqlConditionGroup> configure)
    {
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));
        var nested = new SqlConditionGroup(_resolver);
        configure(nested);
        if (nested.Condition == null)
            return;
        Add(nested.Condition, or: true);
    }

    /// <summary>
    /// 解析并追加一个 Lambda 条件。
    /// </summary>
    /// <param name="predicate">待解析的 Lambda 条件。</param>
    /// <param name="or">是否使用 Or 连接当前条件。</param>
    /// <param name="aliases">条件来源别名列表。</param>
    private void Add(LambdaExpression predicate, bool or, IReadOnlyList<string> aliases = null)
    {
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));
        Add(_resolver(predicate, aliases), or);
    }

    /// <summary>
    /// 将结构化条件追加到当前条件树。
    /// </summary>
    /// <param name="condition">待追加的条件。</param>
    /// <param name="or">是否使用 Or 连接当前条件。</param>
    private void Add(ICondition condition, bool or)
    {
        if (string.IsNullOrWhiteSpace(condition?.GetCondition()))
            return;
        _condition = _condition == null
            ? condition
            : or
                ? new OrCondition(_condition, condition)
                : new AndCondition(_condition, condition);
    }
}

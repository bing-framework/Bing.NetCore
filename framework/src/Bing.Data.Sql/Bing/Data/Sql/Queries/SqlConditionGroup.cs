using System.Linq.Expressions;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql;

internal sealed class SqlConditionGroup : ISqlConditionGroup
{
    private readonly Func<LambdaExpression, ICondition> _resolver;
    private ICondition _condition;

    internal SqlConditionGroup(Func<LambdaExpression, ICondition> resolver)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    internal ICondition Condition => _condition;

    public void And<TEntity>(Expression<Func<TEntity, bool>> predicate) where TEntity : class =>
        Add(predicate, or: false);

    public void And<TFirst, TSecond>(Expression<Func<TFirst, TSecond, bool>> predicate)
        where TFirst : class where TSecond : class => Add(predicate, or: false);

    public void Or<TEntity>(Expression<Func<TEntity, bool>> predicate) where TEntity : class =>
        Add(predicate, or: true);

    public void Or<TFirst, TSecond>(Expression<Func<TFirst, TSecond, bool>> predicate)
        where TFirst : class where TSecond : class => Add(predicate, or: true);

    public void Group(Action<ISqlConditionGroup> configure)
    {
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));
        var nested = new SqlConditionGroup(_resolver);
        configure(nested);
        if (nested.Condition == null)
            return;
        Add(nested.Condition, or: false);
    }

    private void Add(LambdaExpression predicate, bool or)
    {
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));
        Add(_resolver(predicate), or);
    }

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

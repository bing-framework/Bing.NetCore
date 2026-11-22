using System.Linq.Expressions;

namespace Bing.Data.Sql;

public partial class SqlLambdaQuery
{
    /// <summary>使用显式值追加复合查询条件。</summary>
    /// <typeparam name="TEntity">来源实体类型。</typeparam>
    /// <typeparam name="TValue">条件值类型。</typeparam>
    /// <param name="value">本次查询使用的值。</param>
    /// <param name="predicate">第一个参数是实体，第二个参数是显式值的条件表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery Where<TEntity, TValue>(TValue value, Expression<Func<TEntity, TValue, bool>> predicate) =>
        Where(value, predicate, null);

    /// <summary>使用显式值和来源别名追加复合查询条件。</summary>
    /// <typeparam name="TEntity">来源实体类型。</typeparam>
    /// <typeparam name="TValue">条件值类型。</typeparam>
    /// <param name="value">本次查询使用的值。</param>
    /// <param name="predicate">第一个参数是实体，第二个参数是显式值的条件表达式。</param>
    /// <param name="alias">要绑定的来源别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery Where<TEntity, TValue>(TValue value, Expression<Func<TEntity, TValue, bool>> predicate,
        string alias)
    {
        var body = new ExplicitPredicateValueVisitor(predicate.Parameters[1], Expression.Constant(value, typeof(TValue)))
            .Visit(predicate.Body);
        var bound = Expression.Lambda<Func<TEntity, bool>>(body, predicate.Parameters[0]);
        return alias == null ? Where(bound) : Where(bound, alias);
    }

    /// <summary>满足开关条件时使用显式值追加复合查询条件。</summary>
    /// <typeparam name="TEntity">来源实体类型。</typeparam>
    /// <typeparam name="TValue">条件值类型。</typeparam>
    /// <param name="condition">是否追加条件。</param>
    /// <param name="value">本次查询使用的值。</param>
    /// <param name="predicate">第一个参数是实体，第二个参数是显式值的条件表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery WhereIf<TEntity, TValue>(bool condition, TValue value,
        Expression<Func<TEntity, TValue, bool>> predicate) =>
        condition ? Where(value, predicate) : this;

    /// <summary>满足开关条件时使用显式值和来源别名追加复合查询条件。</summary>
    /// <typeparam name="TEntity">来源实体类型。</typeparam>
    /// <typeparam name="TValue">条件值类型。</typeparam>
    /// <param name="condition">是否追加条件。</param>
    /// <param name="value">本次查询使用的值。</param>
    /// <param name="predicate">第一个参数是实体，第二个参数是显式值的条件表达式。</param>
    /// <param name="alias">要绑定的来源别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery WhereIf<TEntity, TValue>(bool condition, TValue value,
        Expression<Func<TEntity, TValue, bool>> predicate, string alias) =>
        condition ? Where(value, predicate, alias) : this;

    private sealed class ExplicitPredicateValueVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression _parameter;
        private readonly ConstantExpression _value;

        internal ExplicitPredicateValueVisitor(ParameterExpression parameter, ConstantExpression value)
        {
            _parameter = parameter;
            _value = value;
        }

        protected override Expression VisitParameter(ParameterExpression node) =>
            node == _parameter ? _value : base.VisitParameter(node);
    }
}

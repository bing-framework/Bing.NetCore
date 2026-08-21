using System.Linq.Expressions;
using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql;

/// <summary>
/// 使用10个实体来源构建的强类型 Lambda 查询描述。
/// </summary>
public sealed class SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth> : SqlMultiLambdaQuery
    where TFirst : class
    where TSecond : class
    where TThird : class
    where TFourth : class
    where TFifth : class
    where TSixth : class
    where TSeventh : class
    where TEighth : class
    where TNinth : class
    where TTenth : class
{
    internal SqlLambdaQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder) : this(executor, builder, true)
    {
    }

    internal SqlLambdaQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool initializeRoots) : base(executor, builder)
    {
        if (initializeRoots)
        {
            GetFromClause((ISqlQueryClauseAccessor)GetBuilder()).SetRoots(new[]
            {
                typeof(TFirst),
                typeof(TSecond),
                typeof(TThird),
                typeof(TFourth),
                typeof(TFifth),
                typeof(TSixth),
                typeof(TSeventh),
                typeof(TEighth),
                typeof(TNinth),
                typeof(TTenth)
            });
            GetBuilder().Select<TFirst>();
        }
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth> Where(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth, bool>> predicate)
    {
        WhereCore(predicate);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth> Select(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth, object[]>> columns)
    {
        SelectCore(columns);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth> Select<TProjection>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth, TProjection>> projection)
    {
        SelectTypedCore(projection);
        return this;
    }

    public SqlSubquery<TProjection> SelectSubquery<TProjection>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth, TProjection>> projection, string alias) where TProjection : class =>
        SelectSubqueryCore<TProjection>(projection, alias);

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth> GroupBy(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth, object[]>> columns)
    {
        GroupByCore(columns);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth> Having(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth, bool>> predicate)
    {
        HavingCore(predicate);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth> Skip(int count)
    {
        SkipCore(count);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth> Take(int count)
    {
        TakeCore(count);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth> OrderBy(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth, object[]>> columns, bool desc = false)
    {
        OrderByCore(columns, desc);
        return this;
    }
}

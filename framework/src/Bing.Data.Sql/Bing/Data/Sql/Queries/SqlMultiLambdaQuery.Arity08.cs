using System.Linq.Expressions;
using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql;

/// <summary>
/// 使用8个实体来源构建的强类型 Lambda 查询描述。
/// </summary>
public sealed class SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> : SqlMultiLambdaQuery
    where TFirst : class
    where TSecond : class
    where TThird : class
    where TFourth : class
    where TFifth : class
    where TSixth : class
    where TSeventh : class
    where TEighth : class
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
                typeof(TEighth)
            });
            GetBuilder().Select<TFirst>();
        }
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> Where(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, bool>> predicate)
    {
        WhereCore(predicate);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> Select(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, object[]>> columns)
    {
        SelectCore(columns);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> Select<TProjection>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TProjection>> projection)
    {
        SelectTypedCore(projection);
        return this;
    }

    public SqlSubquery<TProjection> SelectSubquery<TProjection>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TProjection>> projection, string alias) where TProjection : class =>
        SelectSubqueryCore<TProjection>(projection, alias);

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> GroupBy(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, object[]>> columns)
    {
        GroupByCore(columns);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> Having(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, bool>> predicate)
    {
        HavingCore(predicate);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> Skip(int count)
    {
        SkipCore(count);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> Take(int count)
    {
        TakeCore(count);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> OrderBy(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, object[]>> columns, bool desc = false)
    {
        OrderByCore(columns, desc);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth> Join<TNinth>(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, bool>> predicate,
        string alias = null, string schema = null) where TNinth : class
    {
        JoinCore<TNinth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth> LeftJoin<TNinth>(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, bool>> predicate,
        string alias = null, string schema = null) where TNinth : class
    {
        LeftJoinCore<TNinth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth> RightJoin<TNinth>(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, bool>> predicate,
        string alias = null, string schema = null) where TNinth : class
    {
        RightJoinCore<TNinth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth> FullJoin<TNinth>(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, bool>> predicate,
        string alias = null, string schema = null) where TNinth : class
    {
        FullJoinCore<TNinth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth> CrossJoin<TNinth>(string alias = null, string schema = null) where TNinth : class
    {
        CrossJoinCore<TNinth>(alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth> Join<TNinth>(SqlSubquery<TNinth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, bool>> predicate) where TNinth : class
    {
        JoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth> LeftJoin<TNinth>(SqlSubquery<TNinth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, bool>> predicate) where TNinth : class
    {
        LeftJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth> RightJoin<TNinth>(SqlSubquery<TNinth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, bool>> predicate) where TNinth : class
    {
        RightJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth> FullJoin<TNinth>(SqlSubquery<TNinth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, bool>> predicate) where TNinth : class
    {
        FullJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth> CrossJoin<TNinth>(SqlSubquery<TNinth> subquery) where TNinth : class
    {
        CrossJoinCore(subquery);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth>(Executor, GetBuilder(), false);
    }
}

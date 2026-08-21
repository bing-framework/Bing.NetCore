using System.Linq.Expressions;
using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql;

/// <summary>
/// 使用7个实体来源构建的强类型 Lambda 查询描述。
/// </summary>
public sealed class SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> : SqlMultiLambdaQuery
    where TFirst : class
    where TSecond : class
    where TThird : class
    where TFourth : class
    where TFifth : class
    where TSixth : class
    where TSeventh : class
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
                typeof(TSeventh)
            });
            GetBuilder().Select<TFirst>();
        }
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> Where(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, bool>> predicate)
    {
        WhereCore(predicate);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> Select(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, object[]>> columns)
    {
        SelectCore(columns);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> Select<TProjection>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TProjection>> projection)
    {
        SelectTypedCore(projection);
        return this;
    }

    public SqlSubquery<TProjection> SelectSubquery<TProjection>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TProjection>> projection, string alias) where TProjection : class =>
        SelectSubqueryCore<TProjection>(projection, alias);

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> GroupBy(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, object[]>> columns)
    {
        GroupByCore(columns);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> Having(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, bool>> predicate)
    {
        HavingCore(predicate);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> Skip(int count)
    {
        SkipCore(count);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> Take(int count)
    {
        TakeCore(count);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> OrderBy(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, object[]>> columns, bool desc = false)
    {
        OrderByCore(columns, desc);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> Join<TEighth>(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, bool>> predicate,
        string alias = null, string schema = null) where TEighth : class
    {
        JoinCore<TEighth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> LeftJoin<TEighth>(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, bool>> predicate,
        string alias = null, string schema = null) where TEighth : class
    {
        LeftJoinCore<TEighth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> RightJoin<TEighth>(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, bool>> predicate,
        string alias = null, string schema = null) where TEighth : class
    {
        RightJoinCore<TEighth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> FullJoin<TEighth>(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, bool>> predicate,
        string alias = null, string schema = null) where TEighth : class
    {
        FullJoinCore<TEighth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> CrossJoin<TEighth>(string alias = null, string schema = null) where TEighth : class
    {
        CrossJoinCore<TEighth>(alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> Join<TEighth>(SqlSubquery<TEighth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, bool>> predicate) where TEighth : class
    {
        JoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> LeftJoin<TEighth>(SqlSubquery<TEighth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, bool>> predicate) where TEighth : class
    {
        LeftJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> RightJoin<TEighth>(SqlSubquery<TEighth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, bool>> predicate) where TEighth : class
    {
        RightJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> FullJoin<TEighth>(SqlSubquery<TEighth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, bool>> predicate) where TEighth : class
    {
        FullJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth> CrossJoin<TEighth>(SqlSubquery<TEighth> subquery) where TEighth : class
    {
        CrossJoinCore(subquery);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth>(Executor, GetBuilder(), false);
    }
}

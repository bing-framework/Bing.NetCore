using System.Linq.Expressions;
using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql;

/// <summary>
/// 使用6个实体来源构建的强类型 Lambda 查询描述。
/// </summary>
public sealed class SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> : SqlMultiLambdaQuery
    where TFirst : class
    where TSecond : class
    where TThird : class
    where TFourth : class
    where TFifth : class
    where TSixth : class
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
                typeof(TSixth)
            });
            GetBuilder().Select<TFirst>();
        }
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> Where(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, bool>> predicate)
    {
        WhereCore(predicate);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> Select(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, object[]>> columns)
    {
        SelectCore(columns);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> Select<TProjection>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TProjection>> projection)
    {
        SelectTypedCore(projection);
        return this;
    }

    public SqlSubquery<TProjection> SelectSubquery<TProjection>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TProjection>> projection, string alias) where TProjection : class =>
        SelectSubqueryCore<TProjection>(projection, alias);

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> GroupBy(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, object[]>> columns)
    {
        GroupByCore(columns);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> Having(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, bool>> predicate)
    {
        HavingCore(predicate);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> Skip(int count)
    {
        SkipCore(count);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> Take(int count)
    {
        TakeCore(count);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> OrderBy(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, object[]>> columns, bool desc = false)
    {
        OrderByCore(columns, desc);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> Join<TSeventh>(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, bool>> predicate,
        string alias = null, string schema = null) where TSeventh : class
    {
        JoinCore<TSeventh>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> LeftJoin<TSeventh>(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, bool>> predicate,
        string alias = null, string schema = null) where TSeventh : class
    {
        LeftJoinCore<TSeventh>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> RightJoin<TSeventh>(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, bool>> predicate,
        string alias = null, string schema = null) where TSeventh : class
    {
        RightJoinCore<TSeventh>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> FullJoin<TSeventh>(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, bool>> predicate,
        string alias = null, string schema = null) where TSeventh : class
    {
        FullJoinCore<TSeventh>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> CrossJoin<TSeventh>(string alias = null, string schema = null) where TSeventh : class
    {
        CrossJoinCore<TSeventh>(alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> Join<TSeventh>(SqlSubquery<TSeventh> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, bool>> predicate) where TSeventh : class
    {
        JoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> LeftJoin<TSeventh>(SqlSubquery<TSeventh> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, bool>> predicate) where TSeventh : class
    {
        LeftJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> RightJoin<TSeventh>(SqlSubquery<TSeventh> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, bool>> predicate) where TSeventh : class
    {
        RightJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> FullJoin<TSeventh>(SqlSubquery<TSeventh> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, bool>> predicate) where TSeventh : class
    {
        FullJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh> CrossJoin<TSeventh>(SqlSubquery<TSeventh> subquery) where TSeventh : class
    {
        CrossJoinCore(subquery);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh>(Executor, GetBuilder(), false);
    }
}

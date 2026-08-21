using System.Linq.Expressions;
using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql;

/// <summary>
/// 使用5个实体来源构建的强类型 Lambda 查询描述。
/// </summary>
public sealed class SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> : SqlMultiLambdaQuery
    where TFirst : class
    where TSecond : class
    where TThird : class
    where TFourth : class
    where TFifth : class
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
                typeof(TFifth)
            });
            GetBuilder().Select<TFirst>();
        }
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> Where(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, bool>> predicate)
    {
        WhereCore(predicate);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> Select(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, object[]>> columns)
    {
        SelectCore(columns);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> Select<TProjection>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TProjection>> projection)
    {
        SelectTypedCore(projection);
        return this;
    }

    public SqlSubquery<TProjection> SelectSubquery<TProjection>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TProjection>> projection, string alias) where TProjection : class =>
        SelectSubqueryCore<TProjection>(projection, alias);

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> GroupBy(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, object[]>> columns)
    {
        GroupByCore(columns);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> Having(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, bool>> predicate)
    {
        HavingCore(predicate);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> Skip(int count)
    {
        SkipCore(count);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> Take(int count)
    {
        TakeCore(count);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> OrderBy(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, object[]>> columns, bool desc = false)
    {
        OrderByCore(columns, desc);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> Join<TSixth>(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, bool>> predicate,
        string alias = null, string schema = null) where TSixth : class
    {
        JoinCore<TSixth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> LeftJoin<TSixth>(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, bool>> predicate,
        string alias = null, string schema = null) where TSixth : class
    {
        LeftJoinCore<TSixth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> RightJoin<TSixth>(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, bool>> predicate,
        string alias = null, string schema = null) where TSixth : class
    {
        RightJoinCore<TSixth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> FullJoin<TSixth>(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, bool>> predicate,
        string alias = null, string schema = null) where TSixth : class
    {
        FullJoinCore<TSixth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> CrossJoin<TSixth>(string alias = null, string schema = null) where TSixth : class
    {
        CrossJoinCore<TSixth>(alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> Join<TSixth>(SqlSubquery<TSixth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, bool>> predicate) where TSixth : class
    {
        JoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> LeftJoin<TSixth>(SqlSubquery<TSixth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, bool>> predicate) where TSixth : class
    {
        LeftJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> RightJoin<TSixth>(SqlSubquery<TSixth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, bool>> predicate) where TSixth : class
    {
        RightJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> FullJoin<TSixth>(SqlSubquery<TSixth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, bool>> predicate) where TSixth : class
    {
        FullJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth> CrossJoin<TSixth>(SqlSubquery<TSixth> subquery) where TSixth : class
    {
        CrossJoinCore(subquery);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth, TSixth>(Executor, GetBuilder(), false);
    }
}

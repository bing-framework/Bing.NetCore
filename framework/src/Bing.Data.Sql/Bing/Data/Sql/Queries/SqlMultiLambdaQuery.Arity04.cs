using System.Linq.Expressions;
using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql;

/// <summary>
/// 使用4个实体来源构建的强类型 Lambda 查询描述。
/// </summary>
public sealed class SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> : SqlMultiLambdaQuery
    where TFirst : class
    where TSecond : class
    where TThird : class
    where TFourth : class
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
                typeof(TFourth)
            });
            GetBuilder().Select<TFirst>();
        }
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> Where(Expression<Func<TFirst, TSecond, TThird, TFourth, bool>> predicate)
    {
        WhereCore(predicate);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> Select(Expression<Func<TFirst, TSecond, TThird, TFourth, object[]>> columns)
    {
        SelectCore(columns);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> Select<TProjection>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TProjection>> projection)
    {
        SelectTypedCore(projection);
        return this;
    }

    public SqlSubquery<TProjection> SelectSubquery<TProjection>(
        Expression<Func<TFirst, TSecond, TThird, TFourth, TProjection>> projection, string alias) where TProjection : class =>
        SelectSubqueryCore<TProjection>(projection, alias);

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> GroupBy(Expression<Func<TFirst, TSecond, TThird, TFourth, object[]>> columns)
    {
        GroupByCore(columns);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> Having(Expression<Func<TFirst, TSecond, TThird, TFourth, bool>> predicate)
    {
        HavingCore(predicate);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> Skip(int count)
    {
        SkipCore(count);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> Take(int count)
    {
        TakeCore(count);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> OrderBy(Expression<Func<TFirst, TSecond, TThird, TFourth, object[]>> columns, bool desc = false)
    {
        OrderByCore(columns, desc);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> Join<TFifth>(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, bool>> predicate,
        string alias = null, string schema = null) where TFifth : class
    {
        JoinCore<TFifth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> LeftJoin<TFifth>(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, bool>> predicate,
        string alias = null, string schema = null) where TFifth : class
    {
        LeftJoinCore<TFifth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> RightJoin<TFifth>(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, bool>> predicate,
        string alias = null, string schema = null) where TFifth : class
    {
        RightJoinCore<TFifth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> FullJoin<TFifth>(Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, bool>> predicate,
        string alias = null, string schema = null) where TFifth : class
    {
        FullJoinCore<TFifth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> CrossJoin<TFifth>(string alias = null, string schema = null) where TFifth : class
    {
        CrossJoinCore<TFifth>(alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> Join<TFifth>(SqlSubquery<TFifth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, bool>> predicate) where TFifth : class
    {
        JoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> LeftJoin<TFifth>(SqlSubquery<TFifth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, bool>> predicate) where TFifth : class
    {
        LeftJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> RightJoin<TFifth>(SqlSubquery<TFifth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, bool>> predicate) where TFifth : class
    {
        RightJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> FullJoin<TFifth>(SqlSubquery<TFifth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, TFifth, bool>> predicate) where TFifth : class
    {
        FullJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth> CrossJoin<TFifth>(SqlSubquery<TFifth> subquery) where TFifth : class
    {
        CrossJoinCore(subquery);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth, TFifth>(Executor, GetBuilder(), false);
    }
}

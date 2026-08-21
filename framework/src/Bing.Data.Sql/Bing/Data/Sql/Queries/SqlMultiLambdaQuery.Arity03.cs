using System.Linq.Expressions;
using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql;

/// <summary>
/// 使用3个实体来源构建的强类型 Lambda 查询描述。
/// </summary>
public sealed class SqlLambdaQuery<TFirst, TSecond, TThird> : SqlMultiLambdaQuery
    where TFirst : class
    where TSecond : class
    where TThird : class
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
                typeof(TThird)
            });
            GetBuilder().Select<TFirst>();
        }
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird> Where(Expression<Func<TFirst, TSecond, TThird, bool>> predicate)
    {
        WhereCore(predicate);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird> Select(Expression<Func<TFirst, TSecond, TThird, object[]>> columns)
    {
        SelectCore(columns);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird> Select<TProjection>(
        Expression<Func<TFirst, TSecond, TThird, TProjection>> projection)
    {
        SelectTypedCore(projection);
        return this;
    }

    public SqlSubquery<TProjection> SelectSubquery<TProjection>(
        Expression<Func<TFirst, TSecond, TThird, TProjection>> projection, string alias) where TProjection : class =>
        SelectSubqueryCore<TProjection>(projection, alias);

    public SqlLambdaQuery<TFirst, TSecond, TThird> GroupBy(Expression<Func<TFirst, TSecond, TThird, object[]>> columns)
    {
        GroupByCore(columns);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird> Having(Expression<Func<TFirst, TSecond, TThird, bool>> predicate)
    {
        HavingCore(predicate);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird> Skip(int count)
    {
        SkipCore(count);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird> Take(int count)
    {
        TakeCore(count);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird> OrderBy(Expression<Func<TFirst, TSecond, TThird, object[]>> columns, bool desc = false)
    {
        OrderByCore(columns, desc);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> Join<TFourth>(Expression<Func<TFirst, TSecond, TThird, TFourth, bool>> predicate,
        string alias = null, string schema = null) where TFourth : class
    {
        JoinCore<TFourth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> LeftJoin<TFourth>(Expression<Func<TFirst, TSecond, TThird, TFourth, bool>> predicate,
        string alias = null, string schema = null) where TFourth : class
    {
        LeftJoinCore<TFourth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> RightJoin<TFourth>(Expression<Func<TFirst, TSecond, TThird, TFourth, bool>> predicate,
        string alias = null, string schema = null) where TFourth : class
    {
        RightJoinCore<TFourth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> FullJoin<TFourth>(Expression<Func<TFirst, TSecond, TThird, TFourth, bool>> predicate,
        string alias = null, string schema = null) where TFourth : class
    {
        FullJoinCore<TFourth>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> CrossJoin<TFourth>(string alias = null, string schema = null) where TFourth : class
    {
        CrossJoinCore<TFourth>(alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> Join<TFourth>(SqlSubquery<TFourth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, bool>> predicate) where TFourth : class
    {
        JoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> LeftJoin<TFourth>(SqlSubquery<TFourth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, bool>> predicate) where TFourth : class
    {
        LeftJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> RightJoin<TFourth>(SqlSubquery<TFourth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, bool>> predicate) where TFourth : class
    {
        RightJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> FullJoin<TFourth>(SqlSubquery<TFourth> subquery,
        Expression<Func<TFirst, TSecond, TThird, TFourth, bool>> predicate) where TFourth : class
    {
        FullJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird, TFourth> CrossJoin<TFourth>(SqlSubquery<TFourth> subquery) where TFourth : class
    {
        CrossJoinCore(subquery);
        return new SqlLambdaQuery<TFirst, TSecond, TThird, TFourth>(Executor, GetBuilder(), false);
    }
}

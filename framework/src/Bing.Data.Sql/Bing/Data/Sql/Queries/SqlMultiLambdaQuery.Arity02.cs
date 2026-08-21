using System.Linq.Expressions;
using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql;

/// <summary>
/// 使用2个实体来源构建的强类型 Lambda 查询描述。
/// </summary>
public sealed class SqlLambdaQuery<TFirst, TSecond> : SqlMultiLambdaQuery
    where TFirst : class
    where TSecond : class
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
                typeof(TSecond)
            });
            GetBuilder().Select<TFirst>();
        }
    }

    public SqlLambdaQuery<TFirst, TSecond> Where(Expression<Func<TFirst, TSecond, bool>> predicate)
    {
        WhereCore(predicate);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond> Select(Expression<Func<TFirst, TSecond, object[]>> columns)
    {
        SelectCore(columns);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond> Select<TProjection>(
        Expression<Func<TFirst, TSecond, TProjection>> projection)
    {
        SelectTypedCore(projection);
        return this;
    }

    public SqlSubquery<TProjection> SelectSubquery<TProjection>(
        Expression<Func<TFirst, TSecond, TProjection>> projection, string alias) where TProjection : class =>
        SelectSubqueryCore<TProjection>(projection, alias);

    public SqlLambdaQuery<TFirst, TSecond> GroupBy(Expression<Func<TFirst, TSecond, object[]>> columns)
    {
        GroupByCore(columns);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond> Having(Expression<Func<TFirst, TSecond, bool>> predicate)
    {
        HavingCore(predicate);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond> Skip(int count)
    {
        SkipCore(count);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond> Take(int count)
    {
        TakeCore(count);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond> OrderBy(Expression<Func<TFirst, TSecond, object[]>> columns, bool desc = false)
    {
        OrderByCore(columns, desc);
        return this;
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird> Join<TThird>(Expression<Func<TFirst, TSecond, TThird, bool>> predicate,
        string alias = null, string schema = null) where TThird : class
    {
        JoinCore<TThird>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird> LeftJoin<TThird>(Expression<Func<TFirst, TSecond, TThird, bool>> predicate,
        string alias = null, string schema = null) where TThird : class
    {
        LeftJoinCore<TThird>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird> RightJoin<TThird>(Expression<Func<TFirst, TSecond, TThird, bool>> predicate,
        string alias = null, string schema = null) where TThird : class
    {
        RightJoinCore<TThird>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird> FullJoin<TThird>(Expression<Func<TFirst, TSecond, TThird, bool>> predicate,
        string alias = null, string schema = null) where TThird : class
    {
        FullJoinCore<TThird>(predicate, alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird> CrossJoin<TThird>(string alias = null, string schema = null) where TThird : class
    {
        CrossJoinCore<TThird>(alias, schema);
        return new SqlLambdaQuery<TFirst, TSecond, TThird>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird> Join<TThird>(SqlSubquery<TThird> subquery,
        Expression<Func<TFirst, TSecond, TThird, bool>> predicate) where TThird : class
    {
        JoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird> LeftJoin<TThird>(SqlSubquery<TThird> subquery,
        Expression<Func<TFirst, TSecond, TThird, bool>> predicate) where TThird : class
    {
        LeftJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird> RightJoin<TThird>(SqlSubquery<TThird> subquery,
        Expression<Func<TFirst, TSecond, TThird, bool>> predicate) where TThird : class
    {
        RightJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird> FullJoin<TThird>(SqlSubquery<TThird> subquery,
        Expression<Func<TFirst, TSecond, TThird, bool>> predicate) where TThird : class
    {
        FullJoinCore(subquery, predicate);
        return new SqlLambdaQuery<TFirst, TSecond, TThird>(Executor, GetBuilder(), false);
    }

    public SqlLambdaQuery<TFirst, TSecond, TThird> CrossJoin<TThird>(SqlSubquery<TThird> subquery) where TThird : class
    {
        CrossJoinCore(subquery);
        return new SqlLambdaQuery<TFirst, TSecond, TThird>(Executor, GetBuilder(), false);
    }
}

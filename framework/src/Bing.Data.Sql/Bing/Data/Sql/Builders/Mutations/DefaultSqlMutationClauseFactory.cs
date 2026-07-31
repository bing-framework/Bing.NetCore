using Bing.Data.Sql.Builders.Mutations.Clauses;
using Bing.Data.Sql.Builders.Mutations.Contexts;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 默认 Mutation SQL 子句工厂。
/// </summary>
public sealed class DefaultSqlMutationClauseFactory : ISqlMutationClauseFactory, ISqlUpdateFromClauseFactory,
    ISqlDeleteUsingClauseFactory, ISqlReturningClauseFactory
{
    /// <inheritdoc />
    public IInsertClause CreateInsert(SqlMutationContext context) => new InsertClause(context);

    /// <inheritdoc />
    public IInsertColumnsClause CreateInsertColumns(SqlMutationContext context) => new InsertColumnsClause(context);

    /// <inheritdoc />
    public IValuesClause CreateValues(SqlMutationContext context) => new ValuesClause(context);

    /// <inheritdoc />
    public IUpdateClause CreateUpdate(SqlMutationContext context) => new UpdateClause(context);

    /// <inheritdoc />
    public IUpdateFromClause CreateUpdateFrom(SqlMutationContext context) => new UpdateFromClause(context);

    /// <inheritdoc />
    public ISetClause CreateSet(SqlMutationContext context) => new SetClause(context);

    /// <inheritdoc />
    public IDeleteClause CreateDelete(SqlMutationContext context) => new DeleteClause(context);

    /// <inheritdoc />
    public IDeleteUsingClause CreateDeleteUsing(SqlMutationContext context) => new DeleteUsingClause(context);

    /// <inheritdoc />
    public IReturningClause CreateReturning(SqlMutationContext context) => new ReturningClause(context);

    /// <inheritdoc />
    public IMutationWhereClause CreateWhere(SqlMutationContext context) => new MutationWhereClause(context);
}
using Bing.Data.Sql.Builders.Mutations.Contexts;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 创建写操作 SQL 子句的工厂。
/// </summary>
public interface ISqlMutationClauseFactory
{
    /// <summary>
    /// 创建 Insert 目标表子句。
    /// </summary>
    /// <param name="context">Mutation 子句上下文。</param>
    /// <returns>Insert 目标表子句。</returns>
    IInsertClause CreateInsert(SqlMutationContext context);

    /// <summary>
    /// 创建 Insert 目标列子句。
    /// </summary>
    /// <param name="context">Mutation 子句上下文。</param>
    /// <returns>Insert 目标列子句。</returns>
    IInsertColumnsClause CreateInsertColumns(SqlMutationContext context);

    /// <summary>
    /// 创建 Values 子句。
    /// </summary>
    /// <param name="context">Mutation 子句上下文。</param>
    /// <returns>Values 子句。</returns>
    IValuesClause CreateValues(SqlMutationContext context);

    /// <summary>
    /// 创建 Update 目标表子句。
    /// </summary>
    /// <param name="context">Mutation 子句上下文。</param>
    /// <returns>Update 目标表子句。</returns>
    IUpdateClause CreateUpdate(SqlMutationContext context);

    /// <summary>
    /// 创建 Set 子句。
    /// </summary>
    /// <param name="context">Mutation 子句上下文。</param>
    /// <returns>Set 子句。</returns>
    ISetClause CreateSet(SqlMutationContext context);

    /// <summary>
    /// 创建 Delete 目标表子句。
    /// </summary>
    /// <param name="context">Mutation 子句上下文。</param>
    /// <returns>Delete 目标表子句。</returns>
    IDeleteClause CreateDelete(SqlMutationContext context);

    /// <summary>
    /// 创建 Mutation Where 子句。
    /// </summary>
    /// <param name="context">Mutation 子句上下文。</param>
    /// <returns>Mutation Where 子句。</returns>
    IMutationWhereClause CreateWhere(SqlMutationContext context);
}
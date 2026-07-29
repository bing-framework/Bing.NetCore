namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 由 SQL Provider 可选提供的 Mutation Clause Factory。
/// </summary>
public interface ISqlMutationClauseFactoryProvider
{
    /// <summary>
    /// 当前 Provider 使用的 Mutation Clause Factory。
    /// </summary>
    ISqlMutationClauseFactory MutationClauseFactory { get; }
}
using Bing.Data.Sql.Builders.Mutations.Contexts;

namespace Bing.Data.Sql.Builders.Mutations.Accessors;

/// <summary>
/// 提供 Mutation 运行上下文访问能力。
/// </summary>
public interface ISqlMutationContextAccessor
{
    /// <summary>
    /// 当前 Mutation 运行上下文。
    /// </summary>
    SqlMutationContext MutationContext { get; }
}
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Builders;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 默认 Fluent Mutation SQL Builder 工厂。
/// </summary>
public sealed class SqlFluentMutationBuilderFactory : ISqlFluentMutationBuilderFactory
{
    /// <inheritdoc />
    public ISqlInsertBuilder CreateInsert(ISqlProvider provider, SqlBuilderServices services) =>
        new SqlInsertBuilder(provider, services);

    /// <inheritdoc />
    public ISqlUpdateBuilder CreateUpdate(ISqlProvider provider, SqlBuilderServices services) =>
        new SqlUpdateBuilder(provider, services);

    /// <inheritdoc />
    public ISqlDeleteBuilder CreateDelete(ISqlProvider provider, SqlBuilderServices services) =>
        new SqlDeleteBuilder(provider, services);
}
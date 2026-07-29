using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Builders;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 默认实体写入 Builder 工厂。
/// </summary>
public sealed class SqlMutationBuilderFactory : ISqlMutationBuilderFactory
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

    /// <inheritdoc />
    public ISqlMutationBuilder CreateEntity(ISqlProvider provider, SqlBuilderServices services) =>
        new DefaultSqlMutationBuilder(provider, services);

    /// <inheritdoc />
    public ISqlMutationBuilder Create(ISqlProvider provider, SqlBuilderServices services) =>
        CreateEntity(provider, services);
}
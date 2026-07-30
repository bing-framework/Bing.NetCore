using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 默认实体 Mutation 命令 Builder 工厂。
/// </summary>
public sealed class SqlEntityMutationCommandBuilderFactory : ISqlEntityMutationCommandBuilderFactory
{
    /// <inheritdoc />
    public ISqlEntityMutationCommandBuilder Create(ISqlProvider provider, SqlBuilderServices services) =>
        new DefaultSqlEntityMutationCommandBuilder(provider, services);
}
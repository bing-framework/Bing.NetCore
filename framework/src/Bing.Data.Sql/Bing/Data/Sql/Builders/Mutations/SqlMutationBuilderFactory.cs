using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 默认实体写入 Builder 工厂。
/// </summary>
public sealed class SqlMutationBuilderFactory : ISqlMutationBuilderFactory
{
    /// <inheritdoc />
    public ISqlMutationBuilder Create(ISqlProvider provider, SqlBuilderServices services) =>
        new DefaultSqlMutationBuilder(provider, services);
}
using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 创建基于实体映射的 Mutation 命令 Builder。
/// </summary>
public interface ISqlEntityMutationCommandBuilderFactory
{
    /// <summary>
    /// 创建实体 Mutation 命令 Builder。
    /// </summary>
    /// <param name="provider">当前 SQL Provider。</param>
    /// <param name="services">查询级共享服务。</param>
    /// <returns>独立的实体 Mutation 命令 Builder。</returns>
    ISqlEntityMutationCommandBuilder Create(ISqlProvider provider, SqlBuilderServices services);
}
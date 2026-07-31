using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 创建 Fluent Mutation SQL Builder。
/// </summary>
public interface ISqlFluentMutationBuilderFactory
{
    /// <summary>
    /// 创建 Insert SQL Builder。
    /// </summary>
    /// <param name="provider">决定方言和 Mutation 能力的 SQL Provider。</param>
    /// <param name="services">当前操作使用的 Builder 共享服务。</param>
    /// <returns>绑定 Provider 与共享服务的 Insert Builder。</returns>
    ISqlInsertBuilder CreateInsert(ISqlProvider provider, SqlBuilderServices services);

    /// <summary>
    /// 创建 Update SQL Builder。
    /// </summary>
    /// <param name="provider">决定方言和 Mutation 能力的 SQL Provider。</param>
    /// <param name="services">当前操作使用的 Builder 共享服务。</param>
    /// <returns>绑定 Provider 与共享服务的 Update Builder。</returns>
    ISqlUpdateBuilder CreateUpdate(ISqlProvider provider, SqlBuilderServices services);

    /// <summary>
    /// 创建 Delete SQL Builder。
    /// </summary>
    /// <param name="provider">决定方言和 Mutation 能力的 SQL Provider。</param>
    /// <param name="services">当前操作使用的 Builder 共享服务。</param>
    /// <returns>绑定 Provider 与共享服务的 Delete Builder。</returns>
    ISqlDeleteBuilder CreateDelete(ISqlProvider provider, SqlBuilderServices services);
}

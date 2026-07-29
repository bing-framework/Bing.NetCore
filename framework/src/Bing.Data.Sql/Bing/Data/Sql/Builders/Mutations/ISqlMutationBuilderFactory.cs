using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 创建绑定 Provider 和查询级共享服务的实体写入 Builder。
/// </summary>
public interface ISqlMutationBuilderFactory
{
    /// <summary>
    /// 创建 Insert SQL Builder。
    /// </summary>
    /// <param name="provider">当前 SQL Provider。</param>
    /// <param name="services">查询级共享服务。</param>
    /// <returns>独立的 Insert SQL Builder。</returns>
    ISqlInsertBuilder CreateInsert(ISqlProvider provider, SqlBuilderServices services);

    /// <summary>
    /// 创建 Update SQL Builder。
    /// </summary>
    /// <param name="provider">当前 SQL Provider。</param>
    /// <param name="services">查询级共享服务。</param>
    /// <returns>独立的 Update SQL Builder。</returns>
    ISqlUpdateBuilder CreateUpdate(ISqlProvider provider, SqlBuilderServices services);

    /// <summary>
    /// 创建 Delete SQL Builder。
    /// </summary>
    /// <param name="provider">当前 SQL Provider。</param>
    /// <param name="services">查询级共享服务。</param>
    /// <returns>独立的 Delete SQL Builder。</returns>
    ISqlDeleteBuilder CreateDelete(ISqlProvider provider, SqlBuilderServices services);

    /// <summary>
    /// 创建基于实体映射的 Mutation 适配 Builder。
    /// </summary>
    /// <param name="provider">当前 SQL Provider。</param>
    /// <param name="services">查询级共享服务。</param>
    /// <returns>独立的实体写入 Builder。</returns>
    ISqlMutationBuilder CreateEntity(ISqlProvider provider, SqlBuilderServices services);

    /// <summary>
    /// 创建基于实体映射的 Mutation 适配 Builder。
    /// </summary>
    /// <param name="provider">当前 SQL Provider。</param>
    /// <param name="services">查询级共享服务。</param>
    /// <returns>独立的实体写入 Builder。</returns>
    /// <remarks>兼容现有调用方；新代码应使用 <see cref="CreateEntity"/>。</remarks>
    ISqlMutationBuilder Create(ISqlProvider provider, SqlBuilderServices services);
}
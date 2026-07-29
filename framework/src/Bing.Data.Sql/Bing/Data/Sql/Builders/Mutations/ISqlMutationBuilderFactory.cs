using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 创建绑定 Provider 和查询级共享服务的实体写入 Builder。
/// </summary>
public interface ISqlMutationBuilderFactory
{
    /// <summary>
    /// 创建实体写入 Builder。
    /// </summary>
    /// <param name="provider">当前 SQL Provider。</param>
    /// <param name="services">查询级共享服务。</param>
    /// <returns>独立的实体写入 Builder。</returns>
    ISqlMutationBuilder Create(ISqlProvider provider, SqlBuilderServices services);
}
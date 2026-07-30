using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// SQL Builder 工厂。
/// </summary>
public interface ISqlBuilderFactory
{
    /// <summary>
    /// 根据 Provider 唯一标识创建 Builder。
    /// </summary>
    /// <param name="providerKey">已注册 Provider 的唯一标识；匹配时忽略大小写和首尾空白。</param>
    /// <returns>使用对应 Provider 默认共享服务创建的 SQL Builder。</returns>
    ISqlBuilder Create(string providerKey);

    /// <summary>
    /// 根据 SQL 提供程序创建 Builder。
    /// </summary>
    /// <param name="provider">已注册的 SQL Provider。</param>
    /// <returns>使用对应 Provider 默认共享服务创建的 SQL Builder。</returns>
    ISqlBuilder Create(ISqlProvider provider);

    /// <summary>
    /// 根据 SQL 提供程序和查询级共享服务创建 Builder。
    /// </summary>
    /// <param name="provider">已注册的 SQL Provider。</param>
    /// <param name="services">当前查询的共享服务，包含选项、元数据和数据库上下文解析能力。</param>
    /// <returns>保留指定查询级服务实例的 SQL Builder。</returns>
    ISqlBuilder Create(ISqlProvider provider, SqlBuilderServices services);
}
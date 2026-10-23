using Bing.Uow;

namespace Bing.Data.Sql;

/// <summary>
/// 解析业务工作单元中唯一登记的 SQL 连接资源。
/// </summary>
public interface ISqlUnitOfWorkResourceAccessor
{
    /// <summary>
    /// 获取当前冻结 SQL 上下文对应的资源；允许时可创建由 Dapper 管理的资源。
    /// </summary>
    /// <param name="scope">当前活动的工作单元 root。</param>
    /// <param name="context">调用方捕获的 SQL 上下文。</param>
    /// <param name="connectionStringOverride">可选的已解析连接字符串覆盖值。</param>
    /// <param name="createDapperResource">未映射数据源时是否允许创建由 Dapper 管理的资源。</param>
    /// <param name="requireComparableIdentity">是否要求可比较的物理数据库标识。</param>
    /// <returns>唯一资源；无映射且禁止创建时返回 <see langword="null"/>。</returns>
    IUnitOfWorkConnectionResource GetResource(IUnitOfWorkScope scope, DatabaseContext context,
        string connectionStringOverride = null, bool createDapperResource = true,
        bool requireComparableIdentity = false);
}

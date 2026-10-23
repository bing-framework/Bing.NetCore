using System.Data;

namespace Bing.Uow;

/// <summary>
/// 为显式配置的数据源提供其唯一连接所有者。
/// </summary>
public interface IUnitOfWorkConnectionProvider
{
    /// <summary>
    /// 查找与冻结 SQL 配置匹配的连接资源；未映射的数据源返回 <see langword="null"/>。
    /// </summary>
    /// <param name="scope">当前 root 工作单元。</param>
    /// <param name="dataSourceKey">冻结的数据源键。</param>
    /// <param name="providerKey">冻结的 Provider 键。</param>
    /// <param name="tenantId">冻结的租户标识。</param>
    /// <param name="connectionString">冻结的完整连接配置。</param>
    /// <returns>唯一连接资源；配置冲突必须抛出异常。</returns>
    IUnitOfWorkConnectionResource GetResource(IUnitOfWorkScope scope,
        string dataSourceKey, string providerKey, string tenantId, string connectionString);
}

/// <summary>
/// 暴露由业务工作单元登记并由明确所有者管理的数据库连接和事务。
/// </summary>
public interface IUnitOfWorkConnectionResource : IUnitOfWorkResource
{
    /// <summary>
    /// 获取该资源在当前 root 中使用的登记键。
    /// </summary>
    object ResourceKey { get; }

    /// <summary>
    /// 获取共享数据库连接。
    /// </summary>
    IDbConnection Connection { get; }

    /// <summary>
    /// 获取由该资源拥有的本地事务；非事务资源返回 <see langword="null"/>。
    /// </summary>
    IDbTransaction Transaction { get; }

    /// <summary>
    /// 同步准备连接及本地事务。
    /// </summary>
    void EnsureReady();

    /// <summary>
    /// 异步准备连接及本地事务。
    /// </summary>
    /// <param name="cancellationToken">连接打开和事务开始使用的取消令牌。</param>
    Task EnsureReadyAsync(CancellationToken cancellationToken = default);
}

using System.Data;

namespace Bing.Uow;

/// <summary>
/// 配置业务工作单元的事务行为。
/// </summary>
public sealed class UnitOfWorkScopeOptions
{
    /// <summary>
    /// 获取或设置是否为每个登记资源创建本地事务。
    /// </summary>
    public bool IsTransactional { get; set; } = true;

    /// <summary>
    /// 获取或设置本地事务隔离级别；未指定时使用 Provider 默认值。
    /// </summary>
    public IsolationLevel? IsolationLevel { get; set; }

    /// <summary>
    /// 获取或设置从 root 创建时开始计时的协作取消期限。
    /// </summary>
    public TimeSpan? Timeout { get; set; }
}

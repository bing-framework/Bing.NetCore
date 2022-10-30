using System;
using Bing.DependencyInjection;

namespace Bing.MultiTenancy;

/// <summary>
/// 当前租户
/// </summary>
public class CurrentTenant : ICurrentTenant, ITransientDependency
{
    /// <summary>
    /// 是否可用的
    /// </summary>
    public virtual bool IsAvailable => !string.IsNullOrWhiteSpace(Id);

    /// <summary>
    /// 租户标识
    /// </summary>
    public virtual string Id => _currentTenantAccessor.Current?.TenantId;

    /// <summary>
    /// 租户编码
    /// </summary>
    public virtual string Code => _currentTenantAccessor.Current?.Code;

    /// <summary>
    /// 租户名称
    /// </summary>
    public string Name => _currentTenantAccessor.Current?.Name;

    /// <summary>
    /// 当前租户访问器
    /// </summary>
    private readonly ICurrentTenantAccessor _currentTenantAccessor;

    /// <summary>
    /// 初始化一个<see cref="CurrentTenant"/>类型的实例
    /// </summary>
    /// <param name="currentTenantAccessor">当前租户访问器</param>
    public CurrentTenant(ICurrentTenantAccessor currentTenantAccessor) => _currentTenantAccessor = currentTenantAccessor;

    /// <summary>
    /// 变更
    /// </summary>
    /// <param name="tenantId">租户标识</param>
    /// <param name="code">租户编码</param>
    /// <param name="name">租户名称</param>
    public IDisposable Change(string tenantId, string code = null, string name = null) => SetCurrent(Id, code, name);

    /// <summary>
    /// 设置当前租户信息
    /// </summary>
    /// <param name="tenantId">租户标识</param>
    /// <param name="code">租户编码</param>
    /// <param name="name">租户名称</param>
    private IDisposable SetCurrent(string tenantId, string code = null, string name = null)
    {
        var parentScope = _currentTenantAccessor.Current;
        _currentTenantAccessor.Current = new BasicTenantInfo(tenantId, code, name);
        return new DisposeAction(() =>
        {
            _currentTenantAccessor.Current = parentScope;
        });
    }
}
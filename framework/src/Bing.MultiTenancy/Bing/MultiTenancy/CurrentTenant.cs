using Bing.DependencyInjection;

namespace Bing.MultiTenancy;

/// <summary>
/// 当前租户
/// </summary>
public class CurrentTenant : ICurrentTenant, ITransientDependency
{
    /// <summary>
    /// 当前租户访问器
    /// </summary>
    private readonly ICurrentTenantAccessor _currentTenantAccessor;

    /// <summary>
    /// 是否可用的
    /// </summary>
    public virtual bool IsAvailable => !string.IsNullOrWhiteSpace(Id);

    /// <summary>
    /// 租户标识
    /// </summary>
    public virtual string? Id => _currentTenantAccessor.Current?.TenantId;

    /// <summary>
    /// 租户名称
    /// </summary>
    public string? Name => _currentTenantAccessor.Current?.Name;

    /// <summary>
    /// 初始化一个<see cref="CurrentTenant"/>类型的实例
    /// </summary>
    /// <param name="currentTenantAccessor">当前租户访问器</param>
    public CurrentTenant(ICurrentTenantAccessor currentTenantAccessor) => _currentTenantAccessor = currentTenantAccessor;

    /// <summary>
    /// 变更
    /// </summary>
    /// <param name="id">租户标识</param>
    /// <param name="name">租户名称</param>
    public IDisposable Change(string? id, string? name = null) => SetCurrent(id, name);

    /// <summary>
    /// 设置当前租户信息
    /// </summary>
    /// <param name="tenantId">租户标识</param>
    /// <param name="name">租户名称</param>
    private IDisposable SetCurrent(string? tenantId, string? name = null)
    {
        var parentScope = _currentTenantAccessor.Current;
        _currentTenantAccessor.Current = new BasicTenantInfo(tenantId, name);
        return new DisposeAction<ValueTuple<ICurrentTenantAccessor, BasicTenantInfo?>>(static (state) =>
        {
            var (currentTenantAccessor, parentScope) = state;
            currentTenantAccessor.Current = parentScope;
        }, (_currentTenantAccessor, parentScope));
    }
}

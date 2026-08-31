using Bing.DependencyInjection;

namespace Bing.MultiTenancy;

/// <summary>
/// 基于当前租户访问器公开异步执行流中租户信息的实现。
/// </summary>
public class CurrentTenant : ICurrentTenant, ITransientDependency
{
    /// <summary>
    /// 保存当前异步执行流租户信息的访问器。
    /// </summary>
    private readonly ICurrentTenantAccessor _currentTenantAccessor;

    /// <inheritdoc />
    public virtual bool IsAvailable => !string.IsNullOrWhiteSpace(Id);

    /// <inheritdoc />
    public virtual string? Id => _currentTenantAccessor.Current?.TenantId;

    /// <inheritdoc />
    public string? Name => _currentTenantAccessor.Current?.Name;

    /// <summary>
    /// 使用指定当前租户访问器初始化 <see cref="CurrentTenant"/> 的实例。
    /// </summary>
    /// <param name="currentTenantAccessor">保存当前异步执行流租户信息的访问器。</param>
    public CurrentTenant(ICurrentTenantAccessor currentTenantAccessor) => _currentTenantAccessor = currentTenantAccessor;

    /// <inheritdoc />
    public IDisposable Change(string? id, string? name = null) => SetCurrent(id, name);

    /// <summary>
    /// 切换当前租户并创建可恢复父级上下文的临时作用域。
    /// </summary>
    /// <param name="tenantId">要设置的租户标识，可以为 <c>null</c>。</param>
    /// <param name="name">要设置的租户名称，可以为 <c>null</c>。</param>
    /// <returns>释放后恢复调用前租户上下文的作用域对象。</returns>
    /// <remarks>调用方必须释放返回的作用域对象，才能在嵌套调用结束后恢复父级租户上下文。</remarks>
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

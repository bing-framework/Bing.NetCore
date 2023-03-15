namespace Bing.MultiTenancy;

/// <summary>
/// 基于 <see cref="AsyncLocal{T}"/> 实现的当前租户访问器
/// </summary>
public class AsyncLocalCurrentTenantAccessor : ICurrentTenantAccessor
{
    /// <summary>
    /// 实例
    /// </summary>
    public static AsyncLocalCurrentTenantAccessor Instance { get; } = new();

    /// <summary>
    /// 当前作用域的基本租户信息
    /// </summary>
    private readonly AsyncLocal<BasicTenantInfo> _currentScope;

    /// <summary>
    /// 当前基本租户信息
    /// </summary>
    public BasicTenantInfo Current
    {
        get => _currentScope.Value;
        set => _currentScope.Value = value;
    }

    /// <summary>
    /// 初始化一个<see cref="AsyncLocalCurrentTenantAccessor"/>类型的实例
    /// </summary>
    public AsyncLocalCurrentTenantAccessor() => _currentScope = new AsyncLocal<BasicTenantInfo>();
}

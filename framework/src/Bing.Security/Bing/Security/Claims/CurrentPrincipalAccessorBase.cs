using System.Security.Claims;

namespace Bing.Security.Claims;

/// <summary>
/// 基于 <see cref="AsyncLocal{T}"/> 保存临时身份主体的访问器基类。
/// </summary>
public abstract class CurrentPrincipalAccessorBase : ICurrentPrincipalAccessor
{
    /// <summary>
    /// 保存当前异步执行流临时身份主体的容器。
    /// </summary>
    private readonly AsyncLocal<ClaimsPrincipal> _currentPrincipal = new();

    /// <inheritdoc />
    public ClaimsPrincipal Principal => _currentPrincipal.Value ?? GetClaimsPrincipal();

    /// <summary>
    /// 获取没有临时切换身份主体时使用的默认身份主体。
    /// </summary>
    /// <returns>默认身份主体。</returns>
    protected abstract ClaimsPrincipal GetClaimsPrincipal();

    /// <inheritdoc />
    /// <remarks>临时主体存储在 <see cref="AsyncLocal{T}"/> 中，释放返回作用域后恢复父级执行上下文的主体。</remarks>
    public virtual IDisposable Change(ClaimsPrincipal principal) => SetCurrent(principal);

    /// <summary>
    /// 设置临时身份主体并创建恢复父级主体的作用域。
    /// </summary>
    /// <param name="principal">要在当前作用域使用的身份主体。</param>
    /// <returns>释放后恢复父级身份主体的作用域对象。</returns>
    private IDisposable SetCurrent(ClaimsPrincipal principal)
    {
        var parent = Principal;
        _currentPrincipal.Value = principal;

        return new DisposeAction<ValueTuple<AsyncLocal<ClaimsPrincipal>, ClaimsPrincipal>>(static (state) =>
        {
            var (currentPrincipal, parent) = state;
            currentPrincipal.Value = parent;
        }, (_currentPrincipal, parent));
    }
}

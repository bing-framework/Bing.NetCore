namespace Bing.Threading;

/// <summary>
/// 为当前执行环境提供可临时覆盖取消令牌的基类。
/// </summary>
public abstract class CancellationTokenProviderBase : ICancellationTokenProvider
{
    /// <summary>
    /// 获取在环境作用域中保存取消令牌覆盖值的稳定上下文键。
    /// </summary>
    public const string CancellationTokenOverrideContextKey = "Bing.Threading.CancellationToken.Override";

    /// <summary>
    /// 获取当前有效的异步任务取消令牌。
    /// </summary>
    public abstract CancellationToken Token { get; }

    /// <summary>
    /// 获取保存取消令牌覆盖值的环境范围提供程序。
    /// </summary>
    protected IAmbientScopeProvider<CancellationTokenOverride> CancellationTokenOverrideScopeProvider { get; }

    /// <summary>
    /// 获取当前环境范围中的取消令牌覆盖值。
    /// </summary>
    protected CancellationTokenOverride OverrideValue => CancellationTokenOverrideScopeProvider.GetValue(CancellationTokenOverrideContextKey);

    /// <summary>
    /// 使用取消令牌覆盖范围提供程序初始化 <see cref="CancellationTokenProviderBase"/> 的实例。
    /// </summary>
    /// <param name="cancellationTokenOverrideScopeProvider">保存取消令牌覆盖值的环境范围提供程序。</param>
    protected CancellationTokenProviderBase(IAmbientScopeProvider<CancellationTokenOverride> cancellationTokenOverrideScopeProvider)
    {
        CancellationTokenOverrideScopeProvider = cancellationTokenOverrideScopeProvider;
    }

    /// <summary>
    /// 在当前环境范围内临时使用指定取消令牌。
    /// </summary>
    /// <param name="cancellationToken">要在当前范围内使用的取消令牌。</param>
    /// <returns>释放后恢复先前取消令牌覆盖值的作用域对象。</returns>
    public IDisposable Use(CancellationToken cancellationToken)
    {
        return CancellationTokenOverrideScopeProvider.BeginScope(CancellationTokenOverrideContextKey, new CancellationTokenOverride(cancellationToken));
    }
}

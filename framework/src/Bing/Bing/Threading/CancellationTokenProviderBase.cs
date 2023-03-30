namespace Bing.Threading;

/// <summary>
/// 异步任务取消令牌提供程序基类
/// </summary>
public abstract class CancellationTokenProviderBase : ICancellationTokenProvider
{
    /// <summary>
    /// 取消令牌覆盖上下文键名
    /// </summary>
    public const string CancellationTokenOverrideContextKey = "Bing.Threading.CancellationToken.Override";

    /// <summary>
    /// 异步任务取消令牌
    /// </summary>
    public abstract CancellationToken Token { get; }

    /// <summary>
    /// 取消令牌覆盖-环境范围提供程序
    /// </summary>
    protected IAmbientScopeProvider<CancellationTokenOverride> CancellationTokenOverrideScopeProvider { get; }

    /// <summary>
    /// 取消令牌覆盖值
    /// </summary>
    protected CancellationTokenOverride OverrideValue => CancellationTokenOverrideScopeProvider.GetValue(CancellationTokenOverrideContextKey);

    /// <summary>
    /// 初始化一个<see cref="CancellationTokenProviderBase"/>类型的实例
    /// </summary>
    /// <param name="cancellationTokenOverrideScopeProvider">取消令牌覆盖-环境范围提供程序</param>
    protected CancellationTokenProviderBase(IAmbientScopeProvider<CancellationTokenOverride> cancellationTokenOverrideScopeProvider)
    {
        CancellationTokenOverrideScopeProvider = cancellationTokenOverrideScopeProvider;
    }

    /// <summary>
    /// 自动释放
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public IDisposable Use(CancellationToken cancellationToken)
    {
        return CancellationTokenOverrideScopeProvider.BeginScope(CancellationTokenOverrideContextKey, new CancellationTokenOverride(cancellationToken));
    }
}

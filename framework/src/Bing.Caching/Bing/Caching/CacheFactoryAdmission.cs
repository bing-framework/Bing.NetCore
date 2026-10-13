using System.Runtime.CompilerServices;

namespace Bing.Caching;

/// <summary>
/// 管理缓存回源的并发准入许可。
/// </summary>
/// <remarks>
/// 准入状态按共享资源配置复用，并通过弱表避免永久保留已释放的配置。
/// </remarks>
internal sealed class CacheFactoryAdmission
{
    /// <summary>
    /// 将共享资源配置关联到对应的准入状态。
    /// </summary>
    private static readonly ConditionalWeakTable<CacheResourceOptions, CacheFactoryAdmission> Instances = new();

    /// <summary>
    /// 当前准入状态所使用的资源上限。
    /// </summary>
    private readonly CacheResourceOptions _options;

    /// <summary>
    /// 控制同时执行回源委托数量的许可量。
    /// </summary>
    private readonly SemaphoreSlim _permits;

    /// <summary>
    /// 当前已准入或排队等待许可的请求数。
    /// </summary>
    private int _requests;

    /// <summary>
    /// 初始化一个 <see cref="CacheFactoryAdmission"/> 类型的实例。
    /// </summary>
    /// <param name="options">限制并发数和等待队列容量的资源配置。</param>
    private CacheFactoryAdmission(CacheResourceOptions options)
    {
        _options = options;
        _permits = new SemaphoreSlim(options.MaxConcurrentFactories, options.MaxConcurrentFactories);
    }

    /// <summary>
    /// 为新的回源请求登记队列容量。
    /// </summary>
    private void Register()
    {
        if (Interlocked.Increment(ref _requests) <= (long)_options.MaxConcurrentFactories + _options.MaxQueuedFactories)
            return;
        Interlocked.Decrement(ref _requests);
        throw new InvalidOperationException("缓存回源准入队列已满。");
    }

    /// <summary>
    /// 等待并获取缓存回源准入许可。
    /// </summary>
    /// <param name="options">共享回源资源配置。</param>
    /// <param name="wait">等待许可的最长时长。</param>
    /// <returns>释放时归还准入许可的对象。</returns>
    /// <exception cref="InvalidOperationException">回源准入队列已满时抛出。</exception>
    /// <exception cref="TimeoutException">等待许可超时时抛出。</exception>
    internal static IDisposable Enter(CacheResourceOptions options, TimeSpan wait)
    {
        var admission = Instances.GetValue(options, value => new CacheFactoryAdmission(value));
        admission.Register();
        try
        {
            if (!admission._permits.Wait(wait)) throw new TimeoutException("等待缓存回源准入超时。");
            return new Permit(admission);
        }
        catch { Interlocked.Decrement(ref admission._requests); throw; }
    }

    /// <summary>
    /// 异步等待并获取缓存回源准入许可。
    /// </summary>
    /// <param name="options">共享回源资源配置。</param>
    /// <param name="wait">等待许可的最长时长。</param>
    /// <param name="token">取消等待的令牌。</param>
    /// <returns>异步返回释放时归还准入许可的对象。</returns>
    /// <exception cref="InvalidOperationException">回源准入队列已满时抛出。</exception>
    /// <exception cref="TimeoutException">等待许可超时时抛出。</exception>
    /// <exception cref="OperationCanceledException">等待期间收到取消请求时抛出。</exception>
    internal static async Task<IDisposable> EnterAsync(CacheResourceOptions options, TimeSpan wait, CancellationToken token)
    {
        var admission = Instances.GetValue(options, value => new CacheFactoryAdmission(value));
        admission.Register();
        try
        {
            if (!await admission._permits.WaitAsync(wait, token).ConfigureAwait(false))
                throw new TimeoutException("等待缓存回源准入超时。");
            return new Permit(admission);
        }
        catch { Interlocked.Decrement(ref admission._requests); throw; }
    }

    /// <summary>
    /// 表示持有中的回源准入许可。
    /// </summary>
    private sealed class Permit : IDisposable
    {
        /// <summary>
        /// 持有该许可的准入状态；释放后设为 <see langword="null"/>。
        /// </summary>
        private CacheFactoryAdmission _owner;

        /// <summary>
        /// 初始化一个 <see cref="Permit"/> 类型的实例。
        /// </summary>
        /// <param name="owner">创建该许可的准入状态。</param>
        internal Permit(CacheFactoryAdmission owner) => _owner = owner;

        /// <inheritdoc />
        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            if (owner == null) return;
            Interlocked.Decrement(ref owner._requests);
            owner._permits.Release();
        }
    }
}

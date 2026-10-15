using System.Collections.ObjectModel;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Modularity;

/// <summary>
/// 管理可回收的整组插件，并在重新加载时切换服务入口。
/// </summary>
/// <typeparam name="TStartupModule">应用启动模块。</typeparam>
/// <remarks>
/// 每次重新加载都会建立独立的服务容器和可回收程序集上下文。调用方应通过 RunAsync 使用插件服务，
/// 不得在回调外长期保留插件服务、类型、程序集或服务提供程序。
/// </remarks>
public sealed partial class BingHotPluginHost<TStartupModule> : IAsyncDisposable where TStartupModule : BingModule
{
    /// <summary>
    /// 每一代插件使用的注册配置。
    /// </summary>
    private readonly Action<BingApplicationOptions> _configurePlugins;

    /// <summary>
    /// 每一代插件使用的宿主服务配置。
    /// </summary>
    private readonly Action<IServiceCollection> _configureServices;

    /// <summary>
    /// 串行化重新加载与关闭。
    /// </summary>
    private readonly SemaphoreSlim _reloadGate = new(1, 1);

    /// <summary>
    /// 保护当前代、退役代和宿主关闭状态。
    /// </summary>
    private readonly object _gate = new();

    /// <summary>
    /// 标记当前异步调用是否正在使用本宿主的插件服务。
    /// </summary>
    private readonly AsyncLocal<bool> _insideCall = new();

    /// <summary>
    /// 当前对新调用开放的插件代。
    /// </summary>
    private Generation _current;

    /// <summary>
    /// 正在等待活动调用退出或关闭的插件代。
    /// </summary>
    private readonly List<Generation> _retiring = new();

    /// <summary>
    /// 宿主是否已经关闭。
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 初始化一个 <see cref="BingHotPluginHost{TStartupModule}" /> 类型的实例。
    /// </summary>
    /// <param name="configurePlugins">每次重新加载时执行的插件来源配置。</param>
    /// <param name="configureServices">可选的宿主服务配置。</param>
    public BingHotPluginHost(Action<BingApplicationOptions> configurePlugins,
        Action<IServiceCollection> configureServices = null)
    {
        _configurePlugins = configurePlugins ?? throw new ArgumentNullException(nameof(configurePlugins));
        _configureServices = configureServices;
    }

    /// <summary>
    /// 获取当前插件的轻量信息快照。
    /// </summary>
    public IReadOnlyList<BingHotPluginInfo> Plugins
    {
        get
        {
            lock (_gate) return _current?.Plugins ?? Array.Empty<BingHotPluginInfo>();
        }
    }

    /// <summary>
    /// 获取当前与退役插件代的状态快照。
    /// </summary>
    public BingHotPluginHostStatus Status
    {
        get
        {
            lock (_gate)
                return new BingHotPluginHostStatus(_current?.Snapshot(), _retiring.Select(item => item.Snapshot()));
        }
    }

    /// <summary>
    /// 构建新插件代并切换服务入口。
    /// </summary>
    /// <param name="cancellationToken">构建和初始化的取消令牌。</param>
    /// <remarks>
    /// 新一代初始化失败时保留当前插件代。切换后等待旧调用退出，再关闭旧模块和容器。
    /// 程序集实际回收取决于外部是否仍持有插件对象或程序集引用。
    /// </remarks>
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        var attemptId = Guid.NewGuid();
        var phase = BingHotPluginReloadPhase.WaitForTurn;
        Guid? previousGenerationId = null;
        var switched = false;
        var enteredGate = false;
        try
        {
            if (_insideCall.Value)
                throw new InvalidOperationException("不能在插件服务调用期间等待同一宿主重新加载。");
            await _reloadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            enteredGate = true;
            lock (_gate)
            {
                ThrowIfDisposed();
                previousGenerationId = _current?.Id;
            }
            phase = BingHotPluginReloadPhase.BuildCandidate;
            var next = await BuildGenerationAsync(attemptId, cancellationToken).ConfigureAwait(false);
            Generation previous;
            lock (_gate)
            {
                previous = _current;
                _current = next;
                previous?.Retire();
                if (previous != null) _retiring.Add(previous);
                switched = true;
            }

            // 一旦切换成功，旧代清理必须完成，不能由调用方取消而留下仍运行的旧模块。
            if (previous != null)
            {
                try
                {
                    phase = BingHotPluginReloadPhase.WaitForPreviousCalls;
                    await previous.WaitForIdleAsync().ConfigureAwait(false);
                    phase = BingHotPluginReloadPhase.ClosePrevious;
                    previous.MarkClosing();
                    await previous.CloseAsync().ConfigureAwait(false);
                }
                finally
                {
                    lock (_gate) _retiring.Remove(previous);
                }
            }
        }
        catch (Exception error)
        {
            Guid? currentGenerationId;
            lock (_gate) currentGenerationId = _current?.Id;
            error.Data[BingHotPluginReloadFailureInfo.ExceptionDataKey] =
                new BingHotPluginReloadFailureInfo(attemptId, previousGenerationId,
                    currentGenerationId, phase, switched);
            throw;
        }
        finally { if (enteredGate) _reloadGate.Release(); }
    }

    /// <summary>
    /// 在当前插件代的服务作用域中执行操作。
    /// </summary>
    /// <typeparam name="TResult">操作结果类型。</typeparam>
    /// <param name="action">使用插件服务的操作。</param>
    /// <param name="cancellationToken">操作取消令牌。</param>
    /// <returns>操作结果。</returns>
    public async Task<TResult> RunAsync<TResult>(Func<IServiceProvider, CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken = default)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        Generation generation;
        lock (_gate)
        {
            ThrowIfDisposed();
            generation = _current ?? throw new InvalidOperationException("尚未加载插件，请先调用 ReloadAsync。");
            generation.Acquire();
        }
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var wasInsideCall = _insideCall.Value;
            _insideCall.Value = true;
            try
            {
                await using var scope = generation.Provider.CreateAsyncScope();
                return await action(scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
            }
            finally { _insideCall.Value = wasInsideCall; }
        }
        finally { generation.Release(); }
    }

    /// <inheritdoc />
    /// <remarks>等待当前插件代退出并请求卸载其程序集。</remarks>
    public async ValueTask DisposeAsync()
    {
        if (_insideCall.Value)
            throw new InvalidOperationException("不能在插件服务调用期间等待同一宿主关闭。");
        await _reloadGate.WaitAsync().ConfigureAwait(false);
        try
        {
            Generation previous;
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                previous = _current;
                _current = null;
                previous?.Retire();
                if (previous != null) _retiring.Add(previous);
            }
            if (previous != null)
            {
                try
                {
                    await previous.WaitForIdleAsync().ConfigureAwait(false);
                    previous.MarkClosing();
                    await previous.CloseAsync().ConfigureAwait(false);
                }
                finally
                {
                    lock (_gate) _retiring.Remove(previous);
                }
            }
        }
        finally { _reloadGate.Release(); }
    }

    /// <summary>
    /// 构建并初始化可回收的插件代。
    /// </summary>
    /// <param name="generationId">候选插件代标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>完成初始化的插件代。</returns>
    private async Task<Generation> BuildGenerationAsync(Guid generationId, CancellationToken cancellationToken)
    {
        var context = new AssemblyLoadContext("Bing.HotPlugins." + generationId.ToString("N"), isCollectible: true);
        ServiceProvider provider = null;
        try
        {
            var services = new ServiceCollection();
            _configureServices?.Invoke(services);
            await services.AddBingApplicationAsync<TStartupModule>(options =>
            {
                _configurePlugins(options);
                options.PluginLoadContext = context;
            }, cancellationToken).ConfigureAwait(false);
            provider = services.BuildBingServiceProvider();
            await provider.UseBingAsync(cancellationToken).ConfigureAwait(false);
            var plugins = provider.GetRequiredService<IBingPluginContainer>().Plugins
                .Select(plugin => new BingHotPluginInfo(plugin.Id, plugin.Version, plugin.VersionText)).ToArray();
            return new Generation(generationId, context, provider, new ReadOnlyCollection<BingHotPluginInfo>(plugins));
        }
        catch (Exception error)
        {
            if (provider != null)
            {
                try { await provider.DisposeAsync().ConfigureAwait(false); }
                catch (Exception cleanup) { error.Data["Bing.PluginCleanupError"] = cleanup; }
            }
            context.Unload();
            throw;
        }
    }

    /// <summary>
    /// 拒绝已关闭宿主上的操作。
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(GetType().Name);
    }
}

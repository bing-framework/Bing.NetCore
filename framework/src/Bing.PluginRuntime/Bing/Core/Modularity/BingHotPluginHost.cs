using System.Collections.ObjectModel;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Modularity;

/// <summary>
/// 提供不持有插件程序集引用的当前插件信息。
/// </summary>
public sealed class BingHotPluginInfo
{
    /// <summary>
    /// 初始化插件信息。
    /// </summary>
    /// <param name="id">插件 ID。</param>
    /// <param name="version">插件数值版本。</param>
    /// <param name="versionText">插件完整版本。</param>
    internal BingHotPluginInfo(string id, Version version, string versionText)
    {
        Id = id;
        Version = version;
        VersionText = versionText;
    }

    /// <summary>
    /// 获取插件 ID。
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// 获取插件数值版本，不包含预发布和构建元数据。
    /// </summary>
    public Version Version { get; }

    /// <summary>
    /// 获取插件完整版本。
    /// </summary>
    public string VersionText { get; }
}

/// <summary>
/// 管理可回收的整组插件，并在重新加载时切换服务入口。
/// </summary>
/// <typeparam name="TStartupModule">应用启动模块。</typeparam>
/// <remarks>
/// 每次重新加载都会建立独立的服务容器和可回收程序集上下文。调用方应通过 RunAsync 使用插件服务，
/// 不得在回调外长期保留插件服务、类型、程序集或服务提供程序。
/// </remarks>
public sealed class BingHotPluginHost<TStartupModule> : IAsyncDisposable where TStartupModule : BingModule
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
    /// 保护当前插件代及活动调用数。
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
    /// 宿主是否已经关闭。
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 初始化动态插件宿主。
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
    /// 构建新插件代并切换服务入口。
    /// </summary>
    /// <param name="cancellationToken">构建和初始化的取消令牌。</param>
    /// <returns>旧插件代停止并发出卸载请求后完成的任务。</returns>
    /// <remarks>
    /// 新一代初始化失败时保留当前插件代。切换后等待旧调用退出，再关闭旧模块和容器。
    /// 程序集实际回收取决于外部是否仍持有插件对象或程序集引用。
    /// </remarks>
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        if (_insideCall.Value)
            throw new InvalidOperationException("不能在插件服务调用期间等待同一宿主重新加载。");
        await _reloadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (_gate) ThrowIfDisposed();
            var next = await BuildGenerationAsync(cancellationToken).ConfigureAwait(false);
            Generation previous;
            lock (_gate)
            {
                previous = _current;
                _current = next;
                previous?.Retire();
            }

            // 一旦切换成功，旧代清理必须完成，不能由调用方取消而留下仍运行的旧模块。
            if (previous != null)
            {
                await previous.WaitForIdleAsync().ConfigureAwait(false);
                await previous.CloseAsync().ConfigureAwait(false);
            }
        }
        finally { _reloadGate.Release(); }
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

    /// <summary>
    /// 关闭当前插件代并请求卸载其程序集。
    /// </summary>
    /// <returns>旧调用退出且插件资源释放后的任务。</returns>
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
            }
            if (previous != null)
            {
                await previous.WaitForIdleAsync().ConfigureAwait(false);
                await previous.CloseAsync().ConfigureAwait(false);
            }
        }
        finally { _reloadGate.Release(); }
    }

    /// <summary>
    /// 构建并初始化可回收的插件代。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>完成初始化的插件代。</returns>
    private async Task<Generation> BuildGenerationAsync(CancellationToken cancellationToken)
    {
        var context = new AssemblyLoadContext("Bing.HotPlugins." + Guid.NewGuid().ToString("N"), isCollectible: true);
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
            return new Generation(context, provider, new ReadOnlyCollection<BingHotPluginInfo>(plugins));
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

    /// <summary>
    /// 保存一代插件的容器、上下文与活动调用状态。
    /// </summary>
    private sealed class Generation
    {
        /// <summary>
        /// 保护活动调用计数。
        /// </summary>
        private readonly object _gate = new();

        /// <summary>
        /// 旧代全部调用退出时完成。
        /// </summary>
        private readonly TaskCompletionSource<bool> _idle = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 当前活动调用数。
        /// </summary>
        private int _active;

        /// <summary>
        /// 是否已禁止新调用。
        /// </summary>
        private bool _retired;

        /// <summary>
        /// 初始化插件代。
        /// </summary>
        internal Generation(AssemblyLoadContext context, ServiceProvider provider,
            IReadOnlyList<BingHotPluginInfo> plugins)
        {
            Context = context;
            Provider = provider;
            Plugins = plugins;
        }

        /// <summary>
        /// 获取程序集加载上下文。
        /// </summary>
        internal AssemblyLoadContext Context { get; }

        /// <summary>
        /// 获取本代服务容器。
        /// </summary>
        internal ServiceProvider Provider { get; }

        /// <summary>
        /// 获取插件信息快照。
        /// </summary>
        internal IReadOnlyList<BingHotPluginInfo> Plugins { get; }

        /// <summary>
        /// 登记当前调用。
        /// </summary>
        internal void Acquire()
        {
            lock (_gate)
            {
                if (_retired) throw new InvalidOperationException("插件代已停止接收新调用。");
                _active++;
            }
        }

        /// <summary>
        /// 释放当前调用。
        /// </summary>
        internal void Release()
        {
            lock (_gate)
            {
                _active--;
                if (_retired && _active == 0) _idle.TrySetResult(true);
            }
        }

        /// <summary>
        /// 停止接收新调用。
        /// </summary>
        internal void Retire()
        {
            lock (_gate)
            {
                _retired = true;
                if (_active == 0) _idle.TrySetResult(true);
            }
        }

        /// <summary>
        /// 等待全部已开始的调用退出。
        /// </summary>
        internal Task WaitForIdleAsync() => _idle.Task;

        /// <summary>
        /// 关闭模块、释放服务并请求卸载程序集。
        /// </summary>
        internal async Task CloseAsync()
        {
            var errors = new List<Exception>();
            try { await Provider.ShutdownBingAsync().ConfigureAwait(false); }
            catch (Exception error) { errors.Add(error); }
            try { await Provider.DisposeAsync().ConfigureAwait(false); }
            catch (Exception error) { errors.Add(error); }
            Context.Unload();
            if (errors.Count > 0)
                throw new AggregateException("插件代关闭失败；已尝试释放容器并请求卸载程序集。", errors);
        }
    }
}

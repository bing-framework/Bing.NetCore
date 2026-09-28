using Bing.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Modularity;

/// <summary>
/// 管理单个应用的模块生命周期状态。
/// </summary>
/// <remarks>
/// 管理器不持有初始化作用域中的服务。
/// </remarks>
internal sealed class BingModuleManager : IBingModuleManager, IDisposable, IAsyncDisposable
{
    /// <summary>
    /// 模块运行时状态。
    /// </summary>
    private enum State
    {
        /// <summary>
        /// 已完成服务配置。
        /// </summary>
        Configured,
        /// <summary>
        /// 正在初始化模块。
        /// </summary>
        Initializing,
        /// <summary>
        /// 模块已初始化。
        /// </summary>
        Initialized,
        /// <summary>
        /// 正在关闭模块。
        /// </summary>
        Stopping,
        /// <summary>
        /// 模块已关闭。
        /// </summary>
        Stopped,
        /// <summary>
        /// 初始化或配置已失败。
        /// </summary>
        Failed
    }

    /// <summary>
    /// 应用根服务提供程序。
    /// </summary>
    private readonly IServiceProvider _provider;

    /// <summary>
    /// 当前应用的模块注册记录。
    /// </summary>
    private readonly BingModuleRegistration _registration;

    /// <summary>
    /// 保护生命周期状态和并发请求。
    /// </summary>
    private readonly object _gate = new();

    /// <summary>
    /// 标记当前执行上下文是否位于模块钩子内。
    /// </summary>
    private readonly AsyncLocal<bool> _insideHook = new();

    /// <summary>
    /// 已开始初始化的模块，按初始化顺序保存。
    /// </summary>
    private readonly List<BingModule> _started = new();

    /// <summary>
    /// 当前初始化使用的固定模块序列。
    /// </summary>
    private BingModule[] _initializationOrder;

    /// <summary>
    /// 当前生命周期状态。
    /// </summary>
    private State _state;

    /// <summary>
    /// 当前异步初始化任务。
    /// </summary>
    private Task _initialization;

    /// <summary>
    /// 当前异步关闭任务。
    /// </summary>
    private Task _shutdown;

    /// <summary>
    /// 请求释放容器时用于取消初始化的令牌源。
    /// </summary>
    private readonly CancellationTokenSource _disposeCancellation = new();

    /// <summary>
    /// 标记当前初始化是否由 Web 宿主触发。
    /// </summary>
    private bool _webInitialization;

    /// <summary>
    /// 标记根服务提供程序是否正在释放。
    /// </summary>
    private volatile bool _providerDisposing;

    /// <summary>
    /// 初始化模块生命周期管理器。
    /// </summary>
    /// <param name="provider">应用根服务提供程序。</param>
    /// <param name="registration">当前应用的模块注册记录。</param>
    internal BingModuleManager(IServiceProvider provider, BingModuleRegistration registration)
    {
        registration.Attach(provider);
        _provider = provider;
        _registration = registration;
    }

    /// <inheritdoc />
    public void Initialize(BingModuleInitializationContext context = null)
    {
        TaskCompletionSource<bool> completion;
        lock (_gate)
        {
            if (_state == State.Initialized) return;
            EnsureCanInitialize();
            if (_registration.Modules.Any(m => BingModuleHooks.RequiresAsyncInitialization(m.Instance)))
                throw new InvalidOperationException("应用含异步初始化模块，请使用 UseBingAsync。");
            if (NeedsAsyncShutdown())
                throw new InvalidOperationException("应用含仅异步清理模块，请使用 UseBingAsync，以便启动失败时完成清理。");
            _state = State.Initializing;
            _webInitialization = context?.HostContext != null;
            completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _initialization = completion.Task;
        }
        try
        {
            InitializeCoreAsync(context, false, _disposeCancellation.Token).GetAwaiter().GetResult();
            completion.TrySetResult(true);
        }
        catch (Exception ex)
        {
            completion.TrySetException(ex);
            _ = completion.Task.Exception; // 同步调用方通过抛异常观察失败，避免留下未观察任务。
            throw;
        }
    }

    /// <inheritdoc />
    public Task InitializeAsync(BingModuleInitializationContext context = null, CancellationToken cancellationToken = default)
    {
        TaskCompletionSource<bool> completion;
        Task initialization;
        lock (_gate)
        {
            if (_insideHook.Value) throw new InvalidOperationException("模块初始化不允许重入。");
            if (_state == State.Initialized) return Task.CompletedTask;
            if (_state == State.Initializing && !_webInitialization && context?.HostContext == null && _initialization != null)
                return _initialization;
            EnsureCanInitialize();
            cancellationToken.ThrowIfCancellationRequested();
            _state = State.Initializing;
            _webInitialization = context?.HostContext != null;
            completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            initialization = AwaitInitializationAsync(completion.Task);
            _initialization = initialization;
        }
        // 在锁外进入用户钩子；先发布任务才能正确处理并发和重入。
        _ = CompleteInitializationAsync(completion, context, cancellationToken);
        return initialization;
    }

    /// <summary>
    /// 保留初始化结果的原始异常及取消状态。
    /// </summary>
    private static async Task AwaitInitializationAsync(Task completion) =>
        await completion.ConfigureAwait(false);

    /// <summary>
    /// 完成异步初始化并发布结果。
    /// </summary>
    /// <param name="completion">初始化结果源。</param>
    /// <param name="context">模块初始化上下文。</param>
    /// <param name="cancellationToken">用于取消初始化的令牌。</param>
    private async Task CompleteInitializationAsync(TaskCompletionSource<bool> completion,
        BingModuleInitializationContext context, CancellationToken cancellationToken)
    {
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCancellation.Token);
            await InitializeCoreAsync(context, true, linked.Token).ConfigureAwait(false);
            completion.TrySetResult(true);
        }
        catch (Exception ex) { completion.TrySetException(ex); }
    }

    /// <summary>
    /// 验证当前状态是否允许开始初始化。
    /// </summary>
    private void EnsureCanInitialize()
    {
        if (_state != State.Configured || _insideHook.Value)
            throw new InvalidOperationException($"当前模块状态 {_state} 不允许初始化或重入，请勿重用失败的应用。");
    }

    /// <summary>
    /// 执行模块初始化及初始化失败清理。
    /// </summary>
    /// <param name="supplied">调用方提供的初始化上下文。</param>
    /// <param name="asynchronous">是否使用异步生命周期钩子。</param>
    /// <param name="token">初始化取消令牌。</param>
    private async Task InitializeCoreAsync(BingModuleInitializationContext supplied, bool asynchronous, CancellationToken token)
    {
        IServiceScope scope = null;
        var currentModule = (BingModule)null;
        var currentPhase = "Initialize";
        _insideHook.Value = true;
        try
        {
            _registration.BindLegacy(_provider);
            if (!_registration.Legacy) scope = _provider.CreateScope();
            var provider = scope?.ServiceProvider ?? _provider;
            var context = new BingModuleInitializationContext(provider, supplied?.HostContext, supplied?.InitializeModule);
            var modules = _registration.Modules.Select(d => d.Instance);
            // 旧 Web 入口保留原来的 Level/Order/FullName 规则，非 Web 保留注册顺序。
            if (_registration.Legacy && supplied?.HostContext != null)
                modules = modules.OrderBy(m => m.Level).ThenBy(m => m.Order).ThenBy(m => m.GetType().FullName);
            var ordered = modules.ToArray();
            _initializationOrder = ordered;
            foreach (var phase in new[] { "PreInitialize", "Initialize", "PostInitialize" })
            {
                currentPhase = phase;
                foreach (var module in ordered)
                {
                    token.ThrowIfCancellationRequested();
                    currentModule = module;
                    if (BingModuleHooks.HasInitializationHook(module, phase) &&
                        !_started.Contains(module, ModuleReferenceComparer.Instance))
                        _started.Add(module); // 失败钩子可能已启动部分资源，仍须清理。
                    await BingModuleHooks.InitializeAsync(module, context, phase, asynchronous, token)
                        .ConfigureAwait(false);
                }
            }
            token.ThrowIfCancellationRequested();
            currentModule = null;
            // 作用域释放也是初始化的一部分，释放失败不能将应用标记为成功。
            var completedScope = scope;
            scope = null;
            await DisposeScopeAsync(completedScope, asynchronous).ConfigureAwait(false);
            FlushStartupLogs();
            lock (_gate) _state = State.Initialized;
        }
        catch (Exception error)
        {
            error.Data["Bing.ModulePhase"] = currentPhase;
            if (currentModule != null) error.Data["Bing.ModuleType"] = currentModule.GetType().FullName;
            try { FlushStartupLogs(); }
            catch (Exception logError) { error.Data["Bing.ModuleLogError"] = logError; }
            var cleanup = await CleanupAsync(asynchronous, CancellationToken.None, _providerDisposing).ConfigureAwait(false);
            try { await DisposeScopeAsync(scope, asynchronous).ConfigureAwait(false); }
            catch (Exception scopeError) { cleanup.Add(scopeError); }
            scope = null;
            if (cleanup.Count > 0) error.Data["Bing.ModuleCleanupErrors"] = new AggregateException(cleanup);
            lock (_gate) _state = State.Failed;
            throw;
        }
        finally
        {
            _insideHook.Value = false;
        }
    }

    /// <summary>
    /// 按当前生命周期模式释放初始化或关闭作用域。
    /// </summary>
    /// <param name="scope">需要释放的服务作用域。</param>
    /// <param name="asynchronous">是否优先使用异步释放。</param>
    private static async Task DisposeScopeAsync(IServiceScope scope, bool asynchronous)
    {
        if (asynchronous && scope is IAsyncDisposable asyncScope)
            await asyncScope.DisposeAsync().ConfigureAwait(false);
        else scope?.Dispose();
    }

    /// <summary>
    /// 将启动日志输出到正式日志并清空缓存。
    /// </summary>
    private void FlushStartupLogs() => _provider.GetService<StartupLogger>()?.Output(_provider);

    /// <summary>
    /// 判断当前模块集合是否需要异步关闭。
    /// </summary>
    /// <returns>需要异步关闭时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
    private bool NeedsAsyncShutdown() => _registration.RequiresAsyncDisposal ||
        _registration.Modules.Any(d => BingModuleHooks.RequiresAsyncShutdown(d.Instance));

    /// <inheritdoc />
    public void Shutdown() => ShutdownCore(false, CancellationToken.None, false).GetAwaiter().GetResult();

    /// <inheritdoc />
    public Task ShutdownAsync(CancellationToken cancellationToken = default) => ShutdownCore(true, cancellationToken, false);

    /// <summary>
    /// 启动模块关闭流程并协调重复调用。
    /// </summary>
    /// <param name="asynchronous">是否使用异步关闭钩子。</param>
    /// <param name="token">关闭取消令牌。</param>
    /// <param name="disposingProvider">是否由根服务提供程序释放触发。</param>
    private Task ShutdownCore(bool asynchronous, CancellationToken token, bool disposingProvider)
    {
        TaskCompletionSource<bool> completion;
        lock (_gate)
        {
            if (_insideHook.Value) throw new InvalidOperationException("模块生命周期钩子不能重入关闭流程。");
            if (_state == State.Stopped || _state == State.Failed) return Task.CompletedTask;
            if (_state == State.Initializing) throw new InvalidOperationException("请先等待模块初始化完成，再关闭应用。");
            if (_state == State.Stopping)
            {
                if (asynchronous) return _shutdown;
                throw new InvalidOperationException("模块正在关闭，请等待既有关闭操作。");
            }
            if (!asynchronous && NeedsAsyncShutdown())
                throw new InvalidOperationException("应用需要异步清理，请在释放容器之前调用 ShutdownBingAsync。");
            _state = State.Stopping;
            completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _shutdown = completion.Task;
        }
        _ = CompleteShutdownAsync(completion, asynchronous, token, disposingProvider);
        return completion.Task;
    }

    /// <summary>
    /// 完成异步关闭并发布清理结果。
    /// </summary>
    /// <param name="completion">关闭结果源。</param>
    /// <param name="asynchronous">是否使用异步释放。</param>
    /// <param name="token">关闭取消令牌。</param>
    /// <param name="disposingProvider">是否由根服务提供程序释放触发。</param>
    private async Task CompleteShutdownAsync(TaskCompletionSource<bool> completion, bool asynchronous,
        CancellationToken token, bool disposingProvider)
    {
        _insideHook.Value = true;
        try
        {
            var errors = await CleanupAsync(asynchronous, token, disposingProvider).ConfigureAwait(false);
            lock (_gate) _state = State.Stopped;
            if (errors.Count > 0) completion.TrySetException(new AggregateException("模块关闭发生错误；已继续清理其余模块。", errors));
            else completion.TrySetResult(true);
        }
        catch (Exception ex) { completion.TrySetException(ex); }
        finally
        {
            lock (_gate) _state = State.Stopped;
            _insideHook.Value = false;
        }
    }

    /// <summary>
    /// 按逆序执行模块关闭钩子并释放模块资源。
    /// </summary>
    /// <param name="asynchronous">是否使用异步关闭钩子。</param>
    /// <param name="token">关闭取消令牌。</param>
    /// <param name="disposingProvider">是否由根服务提供程序释放触发。</param>
    /// <returns>清理过程中收集的异常。</returns>
    private async Task<List<Exception>> CleanupAsync(bool asynchronous, CancellationToken token, bool disposingProvider)
    {
        var errors = new List<Exception>();
        IServiceScope scope = null;
        try
        {
            // 容器 Dispose 回调期间根容器已不可解析，不尝试新建作用域。
            if (!disposingProvider)
            {
                try { scope = _provider.CreateScope(); }
                catch (Exception ex) { errors.Add(ex); }
            }
            var context = new BingModuleShutdownContext(scope?.ServiceProvider ?? _provider);
            foreach (var module in (_initializationOrder ?? _started.ToArray()).Reverse()
                         .Where(module => _started.Contains(module, ModuleReferenceComparer.Instance)))
            {
                try
                {
                    await BingModuleHooks.ShutdownAsync(module, context, asynchronous, token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    ex.Data["Bing.ModuleType"] = module.GetType().FullName;
                    ex.Data["Bing.ModulePhase"] = "Shutdown";
                    errors.Add(ex);
                }
            }
            _started.Clear();
            _initializationOrder = null;
        }
        finally
        {
            try
            {
                if (asynchronous && scope is IAsyncDisposable asyncScope)
                    await asyncScope.DisposeAsync().ConfigureAwait(false);
                else scope?.Dispose();
            }
            catch (Exception ex) { errors.Add(ex); }
            errors.AddRange(await _registration.ReleaseAsync(asynchronous).ConfigureAwait(false));
        }
        return errors;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        bool initializing;
        lock (_gate)
        {
            if (_insideHook.Value) throw new InvalidOperationException("模块钩子不能释放所属容器。");
            _providerDisposing = true;
            initializing = _state == State.Initializing;
        }
        if (initializing)
        {
            // 同步释放不能等待异步钩子；发出取消，初始化退出时仍执行完整清理。
            _disposeCancellation.Cancel();
            throw new InvalidOperationException("模块仍在初始化，已请求取消；请等待初始化退出并使用容器 DisposeAsync。");
        }
        ShutdownCore(false, CancellationToken.None, true).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => new(DisposeCoreAsync());

    /// <summary>
    /// 异步释放模块管理器及其所属模块资源。
    /// </summary>
    /// <returns>表示释放操作的值任务。</returns>
    private async Task DisposeCoreAsync()
    {
        Task initialization;
        lock (_gate)
        {
            if (_insideHook.Value) throw new InvalidOperationException("模块钩子不能释放所属容器。");
            _providerDisposing = true;
            initialization = _state == State.Initializing ? _initialization : null;
        }
        if (initialization != null)
        {
            Exception cancellationError = null;
            try { _disposeCancellation.Cancel(); }
            catch (Exception ex) { cancellationError = ex; }
            try { await initialization.ConfigureAwait(false); }
            catch (Exception ex)
            {
                // 初始化调用方仍收到原始异常；释放调用方只报告清理错误。
                if (ex.Data["Bing.ModuleCleanupErrors"] is AggregateException cleanupError)
                    throw cleanupError;
            }
            if (cancellationError != null) throw cancellationError;
        }
        await ShutdownCore(true, CancellationToken.None, true).ConfigureAwait(false);
    }
}

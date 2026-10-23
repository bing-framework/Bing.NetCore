using Bing.Data.Enums;
using Bing.Data.Sql;
using DotNetCore.CAP;
using DotNetCore.CAP.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Bing.Uow;

namespace Bing.Events.Cap;

/// <summary>
/// Owns and starts the isolated CAP service provider for each configured data source.
/// </summary>
internal sealed class CapUnitOfWorkRuntimeManager : IAsyncDisposable
{
    private readonly CapUnitOfWorkRegistrationRegistry _registry;
    private readonly IServiceProvider _rootProvider;
    private readonly ILogger<CapUnitOfWorkRuntimeManager> _logger;
    private readonly Dictionary<string, CapSourceRuntime> _runtimes = new(StringComparer.Ordinal);
    private bool _started;

    public CapUnitOfWorkRuntimeManager(CapUnitOfWorkRegistrationRegistry registry,
        IServiceProvider rootProvider, ILogger<CapUnitOfWorkRuntimeManager> logger)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _rootProvider = rootProvider ?? throw new ArgumentNullException(nameof(rootProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_started)
            return;
        var registrations = _registry.Snapshot();
        if (registrations.Count == 0)
            throw new InvalidOperationException("CAP Uow publisher 至少需要注册一个数据源 runtime。");

        try
        {
            foreach (var registration in registrations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var runtime = BuildRuntime(registration);
                _runtimes.Add(registration.DataSourceKey, runtime);
                await runtime.StartAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("CAP Uow runtime 已启动。DataSource={DataSourceKey}, PublisherOnly={PublisherOnly}",
                    registration.DataSourceKey, registration.PublisherOnly);
            }
            _started = true;
        }
        catch (Exception startException)
        {
            try
            {
                await StopRuntimesAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException("启动 CAP Uow runtime 失败，且清理部分 runtime 时也发生错误。",
                    startException, cleanupException);
            }
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(startException).Throw();
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await StopRuntimesAsync(cancellationToken).ConfigureAwait(false);
        _started = false;
    }

    /// <summary>
    /// Creates a scope from the source-specific CAP container.
    /// </summary>
    internal IServiceScope CreateScope(string dataSourceKey) => GetRuntime(dataSourceKey).CreateScope();

    internal async Task<ICapPublisher> GetOperationPublisherAsync(IUnitOfWorkScope scope,
        string dataSourceKey)
    {
        if (scope == null)
            throw new ArgumentNullException(nameof(scope));
        var runtime = GetRuntime(dataSourceKey);
        var operationScope = scope.GetOrAddResource(runtime.OperationScopeKey, runtime.CreateOperationScope);
        try
        {
            return await operationScope.GetPublisherAsync().ConfigureAwait(false);
        }
        catch
        {
            if (scope.IsActive)
                scope.SetRollbackOnly();
            throw;
        }
    }

    internal string GetEffectiveStorageConnectionString(string dataSourceKey) =>
        GetRuntime(dataSourceKey).StorageConnectionString;

    internal CapUnitOfWorkDataSourceRegistration GetRegistration(string dataSourceKey)
    {
        var registration = _registry.Snapshot().SingleOrDefault(item =>
            string.Equals(item.DataSourceKey, dataSourceKey, StringComparison.Ordinal));
        return registration ?? throw new InvalidOperationException(
            $"未注册数据源 {dataSourceKey} 的 CAP runtime。");
    }

    private CapSourceRuntime GetRuntime(string dataSourceKey)
    {
        if (!_started || !_runtimes.TryGetValue(dataSourceKey, out var runtime))
            throw new InvalidOperationException($"数据源 {dataSourceKey} 的 CAP runtime 尚未启动。");
        return runtime;
    }

    private CapSourceRuntime BuildRuntime(CapUnitOfWorkDataSourceRegistration registration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        AddRootService<IConfiguration>(services);
        AddRootService<IHostEnvironment>(services);
        AddRootService<IHostApplicationLifetime>(services);
        registration.ConfigureServices?.Invoke(services);
        services.AddCapEventBus(options => registration.ConfigureCap(options,
            registration.StorageConnectionString));
        if (registration.PublisherOnly)
            services.EnableBingCapPublisherOnly();
        UseScopedNativePublisher(services);
        IServiceProvider provider = null;
        try
        {
            provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = false
            });
            var effectiveStorageConnectionString = ValidateMySqlStorage(provider, registration);
            return new CapSourceRuntime(provider, registration.DataSourceKey,
                effectiveStorageConnectionString);
        }
        catch (Exception buildException)
        {
            if (provider is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception disposeException)
                {
                    throw new AggregateException("构建 CAP Uow runtime 失败，且释放部分容器时也发生错误。",
                        buildException, disposeException);
                }
            }
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(buildException).Throw();
            throw;
        }
    }

    private string ValidateMySqlStorage(IServiceProvider provider,
        CapUnitOfWorkDataSourceRegistration registration)
    {
        if (registration.DatabaseType != DatabaseType.MySql)
            throw new InvalidOperationException(
                $"CAP Uow 数据源 {registration.DataSourceKey} 必须声明为 MySQL。实际声明：{registration.DatabaseType}。");

        var storage = provider.GetRequiredService<IDataStorage>();
        var providerAssembly = storage.GetType().Assembly;
        var optionsType = providerAssembly?.GetType("DotNetCore.CAP.MySqlOptions", throwOnError: false);
        var storageType = providerAssembly?.GetType("DotNetCore.CAP.MySql.MySqlDataStorage", throwOnError: false);
        if (!string.Equals(providerAssembly.GetName().Name, "DotNetCore.CAP.MySql", StringComparison.Ordinal) ||
            optionsType == null || storageType == null)
            throw new InvalidOperationException(
                $"CAP Uow 数据源 {registration.DataSourceKey} 未加载 CAP 8.0.1 MySQL Provider。" +
                $"实际 IDataStorage={storage.GetType().FullName}, Assembly={providerAssembly.GetName().Name}。");

        if (!storageType.IsInstanceOfType(storage))
            throw new InvalidOperationException(
                $"CAP Uow 数据源 {registration.DataSourceKey} 的最终 IDataStorage 不是原生 MySQL Provider。");

        var optionsServiceType = typeof(Microsoft.Extensions.Options.IOptions<>).MakeGenericType(optionsType);
        var optionsService = provider.GetRequiredService(optionsServiceType);
        var optionsValue = optionsServiceType.GetProperty("Value")?.GetValue(optionsService);
        var connectionString = optionsType.GetProperty("ConnectionString")?.GetValue(optionsValue) as string;
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                $"CAP Uow 数据源 {registration.DataSourceKey} 的最终 MySqlOptions.ConnectionString 为空。");

        EnsureComparableStorageIdentity(registration, registration.StorageConnectionString,
            connectionString, "CAP 最终 MySqlOptions 与声明存储");
        return connectionString;
    }

    private void EnsureComparableStorageIdentity(CapUnitOfWorkDataSourceRegistration registration,
        string expectedConnectionString, string actualConnectionString, string description)
    {
        var resolver = _rootProvider.GetRequiredService<ISqlDatabaseIdentityResolver>();
        var expected = resolver.Resolve(DatabaseType.MySql, expectedConnectionString);
        var actual = resolver.Resolve(DatabaseType.MySql, actualConnectionString);
        if (!expected.IsComparable || expected.IsExclusiveMemory || !actual.IsComparable || actual.IsExclusiveMemory)
            throw new InvalidOperationException(
                $"数据源 {registration.DataSourceKey} 的{description}无法比较物理数据库身份。");
        if (!expected.Equals(actual))
            throw new InvalidOperationException(
                $"数据源 {registration.DataSourceKey} 的{description}指向不同物理数据库。");
    }

    private static void UseScopedNativePublisher(IServiceCollection services)
    {
        var descriptor = services.LastOrDefault(item => item.ServiceType == typeof(ICapPublisher)) ??
                         throw new InvalidOperationException("CAP 未注册原生 ICapPublisher。");

        ServiceDescriptor scopedDescriptor;
        if (descriptor.ImplementationType != null)
            scopedDescriptor = ServiceDescriptor.Scoped(typeof(ICapPublisher), descriptor.ImplementationType);
        else if (descriptor.ImplementationFactory != null)
            scopedDescriptor = ServiceDescriptor.Scoped(typeof(ICapPublisher), descriptor.ImplementationFactory);
        else
            throw new InvalidOperationException("CAP 原生 ICapPublisher 必须由类型或工厂创建，不能复用容器外单例。");

        services.Replace(scopedDescriptor);
    }

    private void AddRootService<TService>(IServiceCollection services) where TService : class
    {
        var instance = _rootProvider.GetService<TService>();
        if (instance != null)
            services.AddSingleton(instance);
    }

    private async Task StopRuntimesAsync(CancellationToken cancellationToken)
    {
        var errors = new List<Exception>();
        foreach (var runtime in _runtimes.Values.Reverse().ToArray())
        {
            try
            {
                await runtime.StopAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
            try
            {
                await runtime.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }
        _runtimes.Clear();
        if (errors.Count == 1)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1)
            throw new AggregateException("停止一个或多个 CAP Uow runtime 失败。", errors);
    }

    public async ValueTask DisposeAsync()
    {
        await StopRuntimesAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private sealed class CapSourceRuntime : IAsyncDisposable
    {
        private readonly IServiceProvider _provider;
        private readonly IHostedService[] _hostedServices;
        private int _startedCount;

        public CapSourceRuntime(IServiceProvider provider, string dataSourceKey, string storageConnectionString)
        {
            _provider = provider;
            DataSourceKey = dataSourceKey;
            StorageConnectionString = storageConnectionString;
            OperationScopeKey = new object();
            _hostedServices = provider.GetServices<IHostedService>().ToArray();
        }

        public string DataSourceKey { get; }
        public string StorageConnectionString { get; }
        public object OperationScopeKey { get; }

        public IServiceScope CreateScope() => _provider.CreateScope();

        public CapOperationScopeResource CreateOperationScope()
        {
            return new CapOperationScopeResource(_provider.CreateAsyncScope());
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            foreach (var service in _hostedServices)
            {
                _startedCount++;
                await service.StartAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            var errors = new List<Exception>();
            for (var index = _startedCount - 1; index >= 0; index--)
            {
                try
                {
                    await _hostedServices[index].StopAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }
            _startedCount = 0;
            if (errors.Count == 1)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1)
                throw new AggregateException($"停止 CAP runtime {DataSourceKey} 失败。", errors);
        }

        public async ValueTask DisposeAsync()
        {
            if (_provider is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            else if (_provider is IDisposable disposable)
                disposable.Dispose();
        }
    }
}

internal sealed class CapOperationScopeResource : Bing.Uow.IUnitOfWorkResource
{
    private readonly AsyncServiceScope _scope;
    private readonly Lazy<Task<ICapPublisher>> _publisher;
    private readonly object _disposeSync = new();
    private Task _disposeTask;

    public CapOperationScopeResource(AsyncServiceScope scope)
    {
        _scope = scope;
        _publisher = new Lazy<Task<ICapPublisher>>(CreatePublisherAsync,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    internal Task<ICapPublisher> GetPublisherAsync() => _publisher.Value;

    private async Task<ICapPublisher> CreatePublisherAsync()
    {
        try
        {
            return _scope.ServiceProvider.GetRequiredService<ICapPublisher>();
        }
        catch (Exception constructionException)
        {
            try
            {
                await DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException(
                    "创建 CAP 操作 publisher 失败，且释放已创建依赖时也发生错误。",
                    constructionException, cleanupException);
            }

            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(constructionException).Throw();
            throw;
        }
    }

    public void Commit() { }
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Rollback() { }
    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void Dispose()
    {
        Task disposal;
        lock (_disposeSync)
        {
            if (_disposeTask == null)
            {
                try
                {
                    _scope.Dispose();
                    _disposeTask = Task.CompletedTask;
                }
                catch (Exception exception)
                {
                    _disposeTask = Task.FromException(exception);
                    throw;
                }
            }

            disposal = _disposeTask.IsCompleted ? Task.CompletedTask : _disposeTask;
        }

        disposal.GetAwaiter().GetResult();
    }

    public ValueTask DisposeAsync()
    {
        lock (_disposeSync)
        {
            if (_disposeTask != null)
                return _disposeTask.IsCompleted ? ValueTask.CompletedTask : new ValueTask(_disposeTask);

            _disposeTask = DisposeScopeAsync();
            return new ValueTask(_disposeTask);
        }
    }

    private async Task DisposeScopeAsync() => await _scope.DisposeAsync().ConfigureAwait(false);
}

internal sealed class CapUnitOfWorkRuntimeHostedService : IHostedService
{
    private readonly CapUnitOfWorkRuntimeManager _runtimeManager;

    public CapUnitOfWorkRuntimeHostedService(CapUnitOfWorkRuntimeManager runtimeManager) =>
        _runtimeManager = runtimeManager ?? throw new ArgumentNullException(nameof(runtimeManager));

    public Task StartAsync(CancellationToken cancellationToken) =>
        _runtimeManager.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) =>
        _runtimeManager.StopAsync(cancellationToken);
}

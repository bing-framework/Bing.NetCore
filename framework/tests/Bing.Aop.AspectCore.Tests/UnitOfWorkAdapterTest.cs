using System.Collections.Concurrent;
using AspectCore.Extensions.Hosting;
using Bing.Application.Aspects;
using Bing.DependencyInjection;
using Bing.Uow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bing.Aop.AspectCore;

/// <summary>
/// 验证中立工作单元标记的 AspectCore 适配行为。
/// </summary>
public sealed class UnitOfWorkAdapterTest
{
    /// <summary>
    /// 成功调用后提交一次并执行提交后回调。
    /// </summary>
    [Fact]
    public async Task Success_ShouldCommitThenRunCommitAfter()
    {
        using var host = CreateHost();
        await host.StartAsync();
        var service = host.Services.GetRequiredService<IUnitOfWorkService>();
        Assert.NotEqual(typeof(UnitOfWorkService), service.GetType());

        await service.ExecuteAsync();

        var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();
        Assert.Equal(new[] { "Target", "Commit", "CommitAfter" }, manager.Events.ToArray());
        Assert.Equal(1, manager.CommitCount);
        Assert.Equal(1, service.CommitAfterCount);
        await host.StopAsync();
    }

    /// <summary>
    /// 验证同步调用也会在目标完成后提交并执行提交后回调。
    /// </summary>
    [Fact]
    public async Task SyncSuccess_ShouldCommitThenRunCommitAfter()
    {
        using var host = CreateHost();
        await host.StartAsync();
        var service = host.Services.GetRequiredService<IUnitOfWorkService>();

        service.ExecuteSync();

        var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();
        Assert.Equal(new[] { "Target", "Commit", "CommitAfter" }, manager.Events.ToArray());
        Assert.Equal(1, manager.CommitCount);
        Assert.Equal(1, service.CommitAfterCount);
        await host.StopAsync();
    }

    /// <summary>
    /// 目标异常时不提交工作单元。
    /// </summary>
    [Fact]
    public async Task Failure_ShouldNotCommit()
    {
        using var host = CreateHost();
        await host.StartAsync();
        var service = host.Services.GetRequiredService<IUnitOfWorkService>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ThrowAsync());

        var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();
        Assert.Equal(0, manager.CommitCount);
        Assert.Equal(0, service.CommitAfterCount);
        await host.StopAsync();
    }

    /// <summary>
    /// 默认 Aspect 范围下嵌套调用只提交一次。
    /// </summary>
    [Fact]
    public async Task Nested_ShouldCommitOnlyAtOuterBoundary()
    {
        using var host = CreateHost();
        await host.StartAsync();
        var service = host.Services.GetRequiredService<IUnitOfWorkService>();

        await service.ExecuteNestedAsync();

        var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();
        Assert.Equal(1, manager.CommitCount);
        Assert.Equal(1, service.CommitAfterCount);
        await host.StopAsync();
    }

    /// <summary>
    /// 验证调用取消时不会提交工作单元。
    /// </summary>
    [Fact]
    public async Task Cancellation_ShouldNotCommit()
    {
        using var host = CreateHost();
        await host.StartAsync();
        var service = host.Services.GetRequiredService<IUnitOfWorkService>();

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.CancelAsync());

        Assert.Equal(0, host.Services.GetRequiredService<RecordingUnitOfWorkManager>().CommitCount);
        await host.StopAsync();
    }

    /// <summary>
    /// 验证提交失败时不会执行提交后回调，并保留提交异常。
    /// </summary>
    [Fact]
    public async Task CommitFailure_ShouldNotRunCommitAfter()
    {
        using var host = CreateHost();
        await host.StartAsync();
        var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();
        manager.ThrowOnCommit = true;
        var service = host.Services.GetRequiredService<IUnitOfWorkService>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync());

        Assert.Equal(1, manager.CommitCount);
        Assert.Equal(0, service.CommitAfterCount);
        await host.StopAsync();
    }

    /// <summary>
    /// 验证 None 范围的嵌套调用分别提交。
    /// </summary>
    [Fact]
    public async Task NoneScope_ShouldCommitEachInvocation()
    {
        using var host = CreateHost();
        await host.StartAsync();
        var service = host.Services.GetRequiredService<IUnitOfWorkService>();

        await service.ExecuteNoneNestedAsync();

        Assert.Equal(2, host.Services.GetRequiredService<RecordingUnitOfWorkManager>().CommitCount);
        await host.StopAsync();
    }

    /// <summary>
    /// 验证 Nested 范围会保留嵌套工作单元边界。
    /// </summary>
    [Fact]
    public async Task NestedScope_ShouldKeepSingleCommitBoundary()
    {
        using var host = CreateHost();
        await host.StartAsync();
        var service = host.Services.GetRequiredService<IUnitOfWorkService>();

        await service.ExecuteNestedScopeAsync();

        var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();
        Assert.Equal(1, manager.CommitCount);
        Assert.Equal(2, manager.Events.Count(x => x == "Target-Nested"));
        await host.StopAsync();
    }

    /// <summary>
    /// 验证工作单元适配器重复注册不会追加服务描述。
    /// </summary>
    [Fact]
    public void EnableBingUnitOfWork_WhenCalledRepeatedly_ShouldRegisterOnce()
    {
        var services = new ServiceCollection();

        services.EnableBingUnitOfWork();
        services.EnableBingUnitOfWork();

        Assert.Single(services, x => x.ServiceType.FullName ==
            "AspectCore.DynamicProxy.IAdditionalInterceptorSelector");
        Assert.Single(services, x => x.ServiceType.FullName ==
            "AspectCore.DependencyInjection.IServiceResolveCallback");
    }

    /// <summary>
    /// 验证不同作用域的并发调用不共享工作单元提交状态。
    /// </summary>
    [Fact]
    public async Task ConcurrentScopes_ShouldCommitIndependently()
    {
        using var host = CreateHost();
        await host.StartAsync();

        await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            using var scope = host.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IUnitOfWorkService>().ExecuteConcurrentAsync();
        }));

        var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();
        Assert.Equal(8, manager.CommitCount);
        await host.StopAsync();
    }

    /// <summary>
    /// 验证接口类型标记可以选择工作单元拦截器。
    /// </summary>
    [Fact]
    public async Task InterfaceTypeMarker_ShouldCommitOnce()
    {
        using var host = CreateHost();
        var service = host.Services.GetRequiredService<IInterfaceTypeMarkedService>();

        await service.ExecuteAsync();

        Assert.Equal(1, host.Services.GetRequiredService<RecordingUnitOfWorkManager>().CommitCount);
    }

    /// <summary>
    /// 验证实现类型标记可以通过接口代理选择工作单元拦截器。
    /// </summary>
    [Fact]
    public async Task ImplementationTypeMarker_ShouldCommitOnce()
    {
        using var host = CreateHost();
        var service = host.Services.GetRequiredService<IImplementationTypeMarkedService>();

        await service.ExecuteAsync();

        Assert.Equal(1, host.Services.GetRequiredService<RecordingUnitOfWorkManager>().CommitCount);
    }

    /// <summary>
    /// 验证接口和实现方法同时标记时不会重复提交。
    /// </summary>
    [Fact]
    public async Task InterfaceAndImplementationMethodMarkers_ShouldCommitOnce()
    {
        using var host = CreateHost();
        var service = host.Services.GetRequiredService<IDuplicateMarkerService>();

        await service.ExecuteAsync();

        Assert.Equal(1, host.Services.GetRequiredService<RecordingUnitOfWorkManager>().CommitCount);
    }

    /// <summary>
    /// 构建启用 AspectCore 与工作单元适配器的测试主机。
    /// </summary>
    /// <returns>测试主机。</returns>
    private static IHost CreateHost()
    {
        return new HostBuilder()
            .UseServiceContext()
            .ConfigureServices(services =>
            {
                services.AddSingleton<RecordingUnitOfWorkManager>();
                services.AddScoped<IUnitOfWorkManager>(provider =>
                    provider.GetRequiredService<RecordingUnitOfWorkManager>());
                services.AddScoped<IUnitOfWorkService, UnitOfWorkService>();
                services.AddScoped<IInterfaceTypeMarkedService, InterfaceTypeMarkedService>();
                services.AddScoped<IImplementationTypeMarkedService, ImplementationTypeMarkedService>();
                services.AddScoped<IDuplicateMarkerService, DuplicateMarkerService>();
                services.EnableAop();
                services.EnableBingUnitOfWork();
            })
            .Build();
    }

    /// <summary>
    /// 为工作单元拦截测试记录目标调用及提交后回调。
    /// </summary>
    public sealed class UnitOfWorkService : IUnitOfWorkService, ICommitAfter
    {
        /// <summary>
        /// 解析测试服务所需依赖的服务提供程序。
        /// </summary>
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// 记录工作单元提交行为的管理器。
        /// </summary>
        private readonly RecordingUnitOfWorkManager _manager;

        /// <summary>
        /// 初始化一个 <see cref="UnitOfWorkService" /> 类型的实例。
        /// </summary>
        /// <param name="serviceProvider">测试服务提供程序。</param>
        /// <param name="manager">工作单元事件记录器。</param>
        public UnitOfWorkService(IServiceProvider serviceProvider, RecordingUnitOfWorkManager manager)
        {
            _serviceProvider = serviceProvider;
            _manager = manager;
        }

        /// <summary>
        /// 记录提交后回调的执行次数。
        /// </summary>
        private int _commitAfterCount;

        /// <summary>
        /// 获取提交后回调的执行次数。
        /// </summary>
        public int CommitAfterCount => Volatile.Read(ref _commitAfterCount);

        /// <inheritdoc />
        [UnitOfWork]
        public Task ExecuteAsync()
        {
            _manager.Record("Target");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        [UnitOfWork]
        public void ExecuteSync() => _manager.Record("Target");

        /// <inheritdoc />
        [UnitOfWork]
        public Task ThrowAsync()
        {
            _manager.Record("Target");
            return Task.FromException(new InvalidOperationException("target failure"));
        }

        /// <inheritdoc />
        [UnitOfWork]
        public async Task CancelAsync()
        {
            _manager.Record("Target");
            await Task.Yield();
            throw new OperationCanceledException();
        }

        /// <inheritdoc />
        [UnitOfWork]
        public async Task ExecuteNestedAsync()
        {
            await _serviceProvider.GetRequiredService<IUnitOfWorkService>().ExecuteInnerAsync();
        }

        /// <inheritdoc />
        [UnitOfWork]
        public Task ExecuteInnerAsync()
        {
            _manager.Record("Target");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        [UnitOfWork(Scope = BingUnitOfWorkScope.None)]
        public async Task ExecuteNoneNestedAsync()
        {
            await _serviceProvider.GetRequiredService<IUnitOfWorkService>().ExecuteNoneInnerAsync();
        }

        /// <inheritdoc />
        [UnitOfWork(Scope = BingUnitOfWorkScope.None)]
        public Task ExecuteNoneInnerAsync()
        {
            _manager.Record("Target");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        [UnitOfWork(Scope = BingUnitOfWorkScope.Nested)]
        public async Task ExecuteNestedScopeAsync()
        {
            _manager.Record("Target-Nested");
            await _serviceProvider.GetRequiredService<IUnitOfWorkService>().ExecuteNestedScopeInnerAsync();
        }

        /// <inheritdoc />
        [UnitOfWork(Scope = BingUnitOfWorkScope.Nested)]
        public Task ExecuteNestedScopeInnerAsync()
        {
            _manager.Record("Target-Nested");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        [UnitOfWork]
        public async Task ExecuteConcurrentAsync()
        {
            await Task.Delay(10);
        }

        /// <inheritdoc />
        public void CommitAfter()
        {
            _manager.Record("CommitAfter");
            Interlocked.Increment(ref _commitAfterCount);
        }
    }

    /// <summary>
    /// 定义工作单元拦截测试使用的服务契约。
    /// </summary>
    public interface IUnitOfWorkService
    {
        /// <summary>
        /// 获取提交后回调的执行次数。
        /// </summary>
        int CommitAfterCount { get; }

        /// <summary>
        /// 执行异步工作单元操作。
        /// </summary>
        Task ExecuteAsync();

        /// <summary>
        /// 执行同步工作单元操作。
        /// </summary>
        void ExecuteSync();

        /// <summary>
        /// 执行并抛出异常的操作。
        /// </summary>
        Task ThrowAsync();

        /// <summary>
        /// 执行并取消的操作。
        /// </summary>
        Task CancelAsync();

        /// <summary>
        /// 执行嵌套工作单元操作。
        /// </summary>
        Task ExecuteNestedAsync();

        /// <summary>
        /// 执行嵌套工作单元中的内部操作。
        /// </summary>
        Task ExecuteInnerAsync();

        /// <summary>
        /// 执行无工作单元嵌套操作。
        /// </summary>
        Task ExecuteNoneNestedAsync();

        /// <summary>
        /// 执行无工作单元嵌套中的内部操作。
        /// </summary>
        Task ExecuteNoneInnerAsync();

        /// <summary>
        /// 执行嵌套范围工作单元操作。
        /// </summary>
        Task ExecuteNestedScopeAsync();

        /// <summary>
        /// 执行嵌套范围工作单元中的内部操作。
        /// </summary>
        Task ExecuteNestedScopeInnerAsync();

        /// <summary>
        /// 并发执行工作单元操作。
        /// </summary>
        Task ExecuteConcurrentAsync();
    }

    /// <summary>
    /// 使用接口类型工作单元标记的服务契约。
    /// </summary>
    [UnitOfWork]
    public interface IInterfaceTypeMarkedService
    {
        /// <summary>
        /// 执行一次业务调用。
        /// </summary>
        Task ExecuteAsync();
    }

    /// <summary>
    /// 使用接口类型工作单元标记的服务实现。
    /// </summary>
    public class InterfaceTypeMarkedService : IInterfaceTypeMarkedService
    {
        /// <inheritdoc />
        public virtual Task ExecuteAsync() => Task.CompletedTask;
    }

    /// <summary>
    /// 使用实现类型工作单元标记的服务契约。
    /// </summary>
    public interface IImplementationTypeMarkedService
    {
        /// <summary>
        /// 执行一次业务调用。
        /// </summary>
        Task ExecuteAsync();
    }

    /// <summary>
    /// 使用实现类型工作单元标记的服务实现。
    /// </summary>
    [UnitOfWork]
    public class ImplementationTypeMarkedService : IImplementationTypeMarkedService
    {
        /// <inheritdoc />
        public virtual Task ExecuteAsync() => Task.CompletedTask;
    }

    /// <summary>
    /// 使用接口和实现方法重复工作单元标记的服务契约。
    /// </summary>
    public interface IDuplicateMarkerService
    {
        /// <summary>
        /// 执行一次业务调用。
        /// </summary>
        [UnitOfWork]
        Task ExecuteAsync();
    }

    /// <summary>
    /// 使用接口和实现方法重复工作单元标记的服务实现。
    /// </summary>
    public class DuplicateMarkerService : IDuplicateMarkerService
    {
        /// <inheritdoc />
        [UnitOfWork]
        public virtual Task ExecuteAsync() => Task.CompletedTask;
    }

    /// <summary>
    /// 记录工作单元提交次数和操作顺序。
    /// </summary>
    public sealed class RecordingUnitOfWorkManager : IUnitOfWorkManager
    {
        /// <summary>
        /// 记录工作单元提交次数。
        /// </summary>
        private int _commitCount;

        /// <summary>
        /// 按顺序保存工作单元事件。
        /// </summary>
        private readonly ConcurrentQueue<string> _events = new();

        /// <summary>
        /// 获取工作单元提交次数。
        /// </summary>
        public int CommitCount => Volatile.Read(ref _commitCount);

        /// <summary>
        /// 获取已记录的工作单元事件。
        /// </summary>
        public IReadOnlyCollection<string> Events => _events.ToArray();

        /// <summary>
        /// 获取或设置提交是否抛出异常。
        /// </summary>
        public bool ThrowOnCommit { get; set; }

        /// <inheritdoc />
        public void Commit() => Interlocked.Increment(ref _commitCount);

        /// <inheritdoc />
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            Record("Commit");
            Interlocked.Increment(ref _commitCount);
            if (ThrowOnCommit)
                throw new InvalidOperationException("commit failure");
            return Task.CompletedTask;
        }

        /// <summary>
        /// 记录一条工作单元事件。
        /// </summary>
        /// <param name="value">事件名称。</param>
        public void Record(string value) => _events.Enqueue(value);

        /// <inheritdoc />
        public void Register(IUnitOfWork unitOfWork)
        {
        }

        /// <inheritdoc />
        public IReadOnlyCollection<IUnitOfWork> GetUnitOfWorks() => Array.Empty<IUnitOfWork>();
    }
}

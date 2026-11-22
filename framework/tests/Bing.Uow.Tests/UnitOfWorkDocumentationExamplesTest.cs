using Bing.Uow;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Uow.Tests;

/// <summary>
/// 编译并运行 Bing.Uow README 中的旧提交与显式 scope 示例。
/// </summary>
public sealed class UnitOfWorkDocumentationExamplesTest
{
    /// <summary>
    /// 验证旧提交示例仅提交一次已登记资源。
    /// </summary>
    [Fact]
    public async Task LegacyCommitExample_ShouldCommitRegisteredWorkOnce()
    {
        var manager = new UnitOfWorkManager();
        var resource = new RecordingUnitOfWork();
        using var cancellation = new CancellationTokenSource();
        manager.Register(resource);

        var example = new LegacyOrderAppService(manager);
        await example.CommitRegisteredWorkAsync(cancellation.Token);

        Assert.Equal(1, resource.CommitAsyncCount);
        Assert.Equal(cancellation.Token, resource.LastCommitToken);
    }

    /// <summary>
    /// 验证作用域示例完成、释放并恢复当前工作单元。
    /// </summary>
    [Fact]
    public async Task ScopeExample_ShouldCompleteAndRestore()
    {
        var (provider, serviceScope, manager) = CreateScopeManager();
        using (provider)
        using (serviceScope)
        {
            var resource = new RecordingResource();
            using var cancellation = new CancellationTokenSource();
            var example = new OrderScopeAppService(manager, () => resource);

            await example.CreateAsync(cancellation.Token);

            Assert.Equal(1, resource.CommitAsyncCount);
            Assert.Equal(cancellation.Token, resource.LastCommitToken);
            Assert.Equal(0, resource.RollbackAsyncCount);
            Assert.Equal(1, resource.DisposeAsyncCount);
            Assert.Null(manager.Current);
        }
    }

    /// <summary>
    /// 验证作用域示例失败时回滚而不提交。
    /// </summary>
    [Fact]
    public async Task ScopeExample_WhenFailed_ShouldNotComplete()
    {
        var (provider, serviceScope, manager) = CreateScopeManager();
        using (provider)
        using (serviceScope)
        {
            var resource = new RecordingResource();
            var example = new OrderScopeAppService(manager, () => resource);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                example.FailAsync(CancellationToken.None));

            Assert.Equal("The example operation failed.", exception.Message);
            Assert.Equal(0, resource.CommitAsyncCount);
            Assert.Equal(1, resource.RollbackAsyncCount);
            Assert.Equal(1, resource.DisposeAsyncCount);
            Assert.Null(manager.Current);
        }
    }

    /// <summary>
    /// 验证执行器示例在作用域开始后解析服务并返回物化结果。
    /// </summary>
    [Fact]
    public async Task OperationExecutorExample_ShouldResolveAfterBeginAndReturnMaterializedResult()
    {
        var services = new ServiceCollection();
        services.AddBingOperations();
        services.AddScoped<OperationExecutorExampleService>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });

        var executor = provider.GetRequiredService<IBingOperationExecutor>();
        var result = await executor.ExecuteAsync<OperationExecutorExampleService, OperationExecutorExampleResult>(
            (service, cancellationToken) => service.CreateAsync(cancellationToken));

        Assert.True(result.ScopeWasActiveWhenServiceResolved);
        Assert.True(result.OperationTokenMatched);
    }

    /// <summary>
    /// 创建工作单元测试所需的容器、作用域及管理器。
    /// </summary>
    /// <returns>测试容器、服务作用域及工作单元管理器。</returns>
    private static (ServiceProvider Provider, IServiceScope Scope, IUnitOfWorkScopeManager Manager)
        CreateScopeManager()
    {
        var services = new ServiceCollection();
        services.AddUnitOfWorkScopes();
        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();
        return (provider, scope, manager);
    }

    /// <summary>
    /// 直接提交已注册工作单元的订单示例应用服务。
    /// </summary>
    public sealed class LegacyOrderAppService
    {
        /// <summary>
        /// 保存旧版订单应用服务使用的工作单元管理器。
        /// </summary>
        private readonly IUnitOfWorkManager _uowManager;

        /// <summary>
        /// 初始化一个 <see cref="LegacyOrderAppService" /> 类型的实例。
        /// </summary>
        /// <param name="uowManager">用于管理工作单元的管理器。</param>
        public LegacyOrderAppService(IUnitOfWorkManager uowManager) => _uowManager = uowManager;

        /// <summary>
        /// 提交已注册到旧工作单元管理器的工作。
        /// </summary>
        /// <param name="cancellationToken">用于取消当前操作的令牌。</param>
        public Task CommitRegisteredWorkAsync(CancellationToken cancellationToken) =>
            _uowManager.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// 在显式工作单元作用域内执行订单操作的示例服务。
    /// </summary>
    public sealed class OrderScopeAppService
    {
        /// <summary>
        /// 保存订单作用域应用服务使用的工作单元作用域管理器。
        /// </summary>
        private readonly IUnitOfWorkScopeManager _scopes;
        /// <summary>
        /// 保存订单作用域应用服务使用的资源工厂。
        /// </summary>
        private readonly Func<IUnitOfWorkResource> _resourceFactory;

        /// <summary>
        /// 初始化一个 <see cref="OrderScopeAppService" /> 类型的实例。
        /// </summary>
        /// <param name="scopes">用于创建依赖注入作用域的工厂。</param>
        /// <param name="resourceFactory">用于创建工作单元测试资源的工厂委托。</param>
        public OrderScopeAppService(IUnitOfWorkScopeManager scopes,
            Func<IUnitOfWorkResource> resourceFactory)
        {
            _scopes = scopes;
            _resourceFactory = resourceFactory;
        }

        /// <summary>
        /// 演示注册资源并完成工作单元作用域。
        /// </summary>
        /// <param name="cancellationToken">用于取消当前操作的令牌。</param>
        public async Task CreateAsync(CancellationToken cancellationToken)
        {
            await using var scope = _scopes.Begin();
            scope.GetOrAddResource("orders", _resourceFactory);
            await scope.CompleteAsync(cancellationToken);
        }

        /// <summary>
        /// 演示作用域内失败后的资源释放。
        /// </summary>
        /// <param name="cancellationToken">用于取消当前操作的令牌。</param>
        public async Task FailAsync(CancellationToken cancellationToken)
        {
            await using var scope = _scopes.Begin();
            scope.GetOrAddResource("orders", _resourceFactory);
            await Task.Yield();
            throw new InvalidOperationException("The example operation failed.");
        }
    }

    /// <summary>
    /// 为UnitOfWorkDocumentationExamplesTest测试记录并观察相关调用状态。
    /// </summary>
    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        /// <summary>
        /// 获取异步提交事务的累计次数。
        /// </summary>
        public int CommitAsyncCount { get; private set; }
        /// <summary>
        /// 获取最近一次提交接收的取消令牌。
        /// </summary>
        public CancellationToken LastCommitToken { get; private set; }

        /// <inheritdoc />
        public int Commit() => 1;

        /// <inheritdoc />
        public Task<int> CommitAsync(CancellationToken cancellationToken = default)
        {
            CommitAsyncCount++;
            LastCommitToken = cancellationToken;
            return Task.FromResult(1);
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }

    /// <summary>
    /// 为UnitOfWorkDocumentationExamplesTest测试记录并观察相关调用状态。
    /// </summary>
    private sealed class RecordingResource : IUnitOfWorkResource
    {
        /// <summary>
        /// 获取异步提交事务的累计次数。
        /// </summary>
        public int CommitAsyncCount { get; private set; }
        /// <summary>
        /// 获取异步回滚事务的累计次数。
        /// </summary>
        public int RollbackAsyncCount { get; private set; }
        /// <summary>
        /// 获取异步释放资源的累计次数。
        /// </summary>
        public int DisposeAsyncCount { get; private set; }
        /// <summary>
        /// 获取最近一次提交接收的取消令牌。
        /// </summary>
        public CancellationToken LastCommitToken { get; private set; }

        /// <inheritdoc />
        public void Commit()
        {
        }

        /// <inheritdoc />
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            CommitAsyncCount++;
            LastCommitToken = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public void Rollback()
        {
        }

        /// <inheritdoc />
        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            RollbackAsyncCount++;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            DisposeAsyncCount++;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// 报告服务解析时工作单元执行作用域状态的示例服务。
    /// </summary>
    public sealed class OperationExecutorExampleService
    {
        /// <summary>
        /// 保存操作执行器示例服务在解析时取得的作用域。
        /// </summary>
        private readonly IUnitOfWorkExecutionScope _scopeAtResolution;

        /// <summary>
        /// 初始化一个 <see cref="OperationExecutorExampleService" /> 类型的实例。
        /// </summary>
        /// <param name="scopeManager">用于管理当前工作单元作用域的管理器。</param>
        public OperationExecutorExampleService(IUnitOfWorkScopeManager scopeManager) =>
            _scopeAtResolution = scopeManager.Current as IUnitOfWorkExecutionScope;

        /// <summary>
        /// 报告服务解析时及执行时的操作上下文。
        /// </summary>
        /// <param name="cancellationToken">用于取消当前操作的令牌。</param>
        /// <returns>服务解析时及执行时的操作上下文观测结果。</returns>
        public Task<OperationExecutorExampleResult> CreateAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new OperationExecutorExampleResult(
                _scopeAtResolution?.IsActive == true,
                _scopeAtResolution?.CancellationToken == cancellationToken));
    }

    /// <summary>
    /// 记录工作单元文档示例验证的作用域和操作令牌结果。
    /// </summary>
    /// <param name="ScopeWasActiveWhenServiceResolved">解析服务时操作作用域是否处于活动状态。</param>
    /// <param name="OperationTokenMatched">实际使用的操作令牌是否与预期匹配。</param>
    public sealed record OperationExecutorExampleResult(bool ScopeWasActiveWhenServiceResolved,
        bool OperationTokenMatched);
}

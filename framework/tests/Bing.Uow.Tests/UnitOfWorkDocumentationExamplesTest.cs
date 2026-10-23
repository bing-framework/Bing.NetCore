using Bing.Uow;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Uow.Tests;

/// <summary>
/// 编译并运行 Bing.Uow README 中的旧提交与显式 scope 示例。
/// </summary>
public sealed class UnitOfWorkDocumentationExamplesTest
{
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

    public sealed class LegacyOrderAppService
    {
        private readonly IUnitOfWorkManager _uowManager;

        public LegacyOrderAppService(IUnitOfWorkManager uowManager) => _uowManager = uowManager;

        public Task CommitRegisteredWorkAsync(CancellationToken cancellationToken) =>
            _uowManager.CommitAsync(cancellationToken);
    }

    public sealed class OrderScopeAppService
    {
        private readonly IUnitOfWorkScopeManager _scopes;
        private readonly Func<IUnitOfWorkResource> _resourceFactory;

        public OrderScopeAppService(IUnitOfWorkScopeManager scopes,
            Func<IUnitOfWorkResource> resourceFactory)
        {
            _scopes = scopes;
            _resourceFactory = resourceFactory;
        }

        public async Task CreateAsync(CancellationToken cancellationToken)
        {
            await using var scope = _scopes.Begin();
            scope.GetOrAddResource("orders", _resourceFactory);
            await scope.CompleteAsync(cancellationToken);
        }

        public async Task FailAsync(CancellationToken cancellationToken)
        {
            await using var scope = _scopes.Begin();
            scope.GetOrAddResource("orders", _resourceFactory);
            await Task.Yield();
            throw new InvalidOperationException("The example operation failed.");
        }
    }

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public int CommitAsyncCount { get; private set; }
        public CancellationToken LastCommitToken { get; private set; }

        public int Commit() => 1;

        public Task<int> CommitAsync(CancellationToken cancellationToken = default)
        {
            CommitAsyncCount++;
            LastCommitToken = cancellationToken;
            return Task.FromResult(1);
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingResource : IUnitOfWorkResource
    {
        public int CommitAsyncCount { get; private set; }
        public int RollbackAsyncCount { get; private set; }
        public int DisposeAsyncCount { get; private set; }
        public CancellationToken LastCommitToken { get; private set; }

        public void Commit()
        {
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            CommitAsyncCount++;
            LastCommitToken = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public void Rollback()
        {
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            RollbackAsyncCount++;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync()
        {
            DisposeAsyncCount++;
            return ValueTask.CompletedTask;
        }
    }

    public sealed class OperationExecutorExampleService
    {
        private readonly IUnitOfWorkExecutionScope _scopeAtResolution;

        public OperationExecutorExampleService(IUnitOfWorkScopeManager scopeManager) =>
            _scopeAtResolution = scopeManager.Current as IUnitOfWorkExecutionScope;

        public Task<OperationExecutorExampleResult> CreateAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new OperationExecutorExampleResult(
                _scopeAtResolution?.IsActive == true,
                _scopeAtResolution?.CancellationToken == cancellationToken));
    }

    public sealed record OperationExecutorExampleResult(bool ScopeWasActiveWhenServiceResolved,
        bool OperationTokenMatched);
}

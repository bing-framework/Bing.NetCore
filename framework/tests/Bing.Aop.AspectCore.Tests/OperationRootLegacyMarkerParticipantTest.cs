using AspectCore.Extensions.Hosting;
using Bing.Application.Aspects;
using Bing.DependencyInjection;
using Bing.Uow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bing.Aop.AspectCore;

/// <summary>
/// 验证旧工作单元标记在显式业务操作 root 中参与新提交协议。
/// </summary>
public sealed class OperationRootLegacyMarkerParticipantTest
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExistingOperationRoot_ShouldTreatMarkedCallsAsParticipants(bool operationsRegisteredFirst)
    {
        using var host = CreateHost(operationsRegisteredFirst);
        using var scope = host.Services.CreateScope();
        var operationManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
        var legacyManager = scope.ServiceProvider.GetRequiredService<LegacyManager>();
        var service = scope.ServiceProvider.GetRequiredService<IRootMarkerProbe>();
        var root = operationManager.BeginOperation();
        var resource = root.GetOrAddResource("operation", () => new CountingResource());

        await service.SucceedAsync();
        service.SucceedSync();

        Assert.Equal(0, legacyManager.CommitCount);
        Assert.Equal(0, service.CommitAfterCount);
        Assert.False(root.IsRollbackOnly);
        await root.CompleteAsync();

        Assert.Equal(1, resource.CommitCount);
        Assert.Equal(0, resource.RollbackCount);
        Assert.Equal(0, legacyManager.CommitCount);
        Assert.Equal(1, service.CommitAfterCount);
        await root.DisposeAsync();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CaughtMarkedCallFailure_ShouldPoisonExistingOperationRoot(bool operationsRegisteredFirst)
    {
        using var host = CreateHost(operationsRegisteredFirst);
        using var scope = host.Services.CreateScope();
        var operationManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
        var legacyManager = scope.ServiceProvider.GetRequiredService<LegacyManager>();
        var service = scope.ServiceProvider.GetRequiredService<IRootMarkerProbe>();
        var root = operationManager.BeginOperation();
        var resource = root.GetOrAddResource("operation", () => new CountingResource());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.FailAsync());

        Assert.True(root.IsRollbackOnly);
        Assert.Equal(0, legacyManager.CommitCount);
        await Assert.ThrowsAsync<InvalidOperationException>(() => root.CompleteAsync());

        Assert.Equal(0, resource.CommitCount);
        Assert.Equal(1, resource.RollbackCount);
        Assert.Equal(0, service.CommitAfterCount);
        await root.DisposeAsync();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MarkedCallOutsideOperationRoot_ShouldKeepLegacyCommit(bool operationsRegisteredFirst)
    {
        using var host = CreateHost(operationsRegisteredFirst);
        using var scope = host.Services.CreateScope();
        var legacyManager = scope.ServiceProvider.GetRequiredService<LegacyManager>();
        var service = scope.ServiceProvider.GetRequiredService<IRootMarkerProbe>();

        await service.SucceedAsync();

        Assert.Equal(1, legacyManager.CommitCount);
        Assert.Equal(1, service.CommitAfterCount);
    }

    private static IHost CreateHost(bool operationsRegisteredFirst)
    {
        return new HostBuilder()
            .UseServiceContext()
            .ConfigureServices(services =>
            {
                services.AddScoped<IRootMarkerProbe, RootMarkerProbe>();
                services.EnableAop();
                if (operationsRegisteredFirst)
                {
                    services.AddBingOperations();
                    services.EnableBingUnitOfWork();
                    services.AddBingOperations();
                    services.EnableBingUnitOfWork();
                }
                else
                {
                    services.EnableBingUnitOfWork();
                    services.AddBingOperations();
                    services.EnableBingUnitOfWork();
                    services.AddBingOperations();
                }
                services.AddScoped<LegacyManager>();
                services.AddScoped<IUnitOfWorkManager>(provider => provider.GetRequiredService<LegacyManager>());
            })
            .Build();
    }

    public interface IRootMarkerProbe : ICommitAfter
    {
        int CommitAfterCount { get; }

        [UnitOfWork]
        Task SucceedAsync();

        [UnitOfWork]
        void SucceedSync();

        [UnitOfWork]
        Task FailAsync();
    }

    public sealed class RootMarkerProbe : IRootMarkerProbe
    {
        private int _commitAfterCount;

        public int CommitAfterCount => Volatile.Read(ref _commitAfterCount);

        public Task SucceedAsync() => Task.CompletedTask;

        public void SucceedSync() { }

        public Task FailAsync() => Task.FromException(new InvalidOperationException("marked failure"));

        public void CommitAfter() => Interlocked.Increment(ref _commitAfterCount);
    }

    public sealed class LegacyManager : IUnitOfWorkManager
    {
        private int _commitCount;

        public int CommitCount => Volatile.Read(ref _commitCount);

        public void Commit() => Interlocked.Increment(ref _commitCount);

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commit();
            return Task.CompletedTask;
        }

        public void Register(IUnitOfWork unitOfWork) { }

        public IReadOnlyCollection<IUnitOfWork> GetUnitOfWorks() => Array.Empty<IUnitOfWork>();
    }

    private sealed class CountingResource : IUnitOfWorkResource
    {
        public int CommitCount { get; private set; }
        public int RollbackCount { get; private set; }

        public void Commit() => CommitCount++;
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commit();
            return Task.CompletedTask;
        }
        public void Rollback() => RollbackCount++;
        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            Rollback();
            return Task.CompletedTask;
        }
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

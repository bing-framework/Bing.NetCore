using Bing.Uow;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Uow.Tests;

/// <summary>
/// 验证应用 operation、后台 worker 迭代和 job 各自使用独立的异步依赖注入范围。
/// </summary>
public sealed class BackgroundScopeConsumerTest
{
    /// <summary>
    /// 验证三个消费者调用各自创建 root 工作单元，并在 scope 退出时清空 Current。
    /// </summary>
    [Fact]
    public async Task OperationWorkerIterationAndJob_ShouldUseFreshScopes()
    {
        var services = new ServiceCollection();
        services.AddUnitOfWorkScopes();
        services.AddScoped<ScopedOperation>();
        await using var provider = services.BuildServiceProvider();

        var operationScope = await RunInNewScopeAsync(provider);
        var workerIterationScope = await RunInNewScopeAsync(provider);
        var jobScope = await RunInNewScopeAsync(provider);

        Assert.NotEqual(operationScope, workerIterationScope);
        Assert.NotEqual(operationScope, jobScope);
        Assert.NotEqual(workerIterationScope, jobScope);
    }

    private static async Task<Guid> RunInNewScopeAsync(IServiceProvider rootProvider)
    {
        await using var serviceScope = rootProvider.CreateAsyncScope();
        var operation = serviceScope.ServiceProvider.GetRequiredService<ScopedOperation>();
        return await operation.ExecuteAsync();
    }

    private sealed class ScopedOperation
    {
        private readonly IUnitOfWorkScopeManager _manager;

        public ScopedOperation(IUnitOfWorkScopeManager manager) => _manager = manager;

        public async Task<Guid> ExecuteAsync()
        {
            Guid id;
            await using (var scope = _manager.Begin())
            {
                id = scope.Id;
                await Task.Yield();
                await scope.CompleteAsync();
            }

            Assert.Null(_manager.Current);
            return id;
        }
    }
}

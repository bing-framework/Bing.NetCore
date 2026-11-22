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

    /// <summary>
    /// 在新的服务作用域中执行操作并返回作用域标识。
    /// </summary>
    /// <param name="rootProvider">用于创建独立服务作用域的根容器。</param>
    /// <returns>本次操作使用的工作单元作用域标识。</returns>
    private static async Task<Guid> RunInNewScopeAsync(IServiceProvider rootProvider)
    {
        await using var serviceScope = rootProvider.CreateAsyncScope();
        var operation = serviceScope.ServiceProvider.GetRequiredService<ScopedOperation>();
        return await operation.ExecuteAsync();
    }

    /// <summary>
    /// 保存后台作用域测试中待处理的应用服务操作。
    /// </summary>
    private sealed class ScopedOperation
    {
        /// <summary>
        /// 保存作用域操作使用的工作单元作用域管理器。
        /// </summary>
        private readonly IUnitOfWorkScopeManager _manager;

        /// <summary>
        /// 初始化一个 <see cref="ScopedOperation" /> 类型的实例。
        /// </summary>
        /// <param name="manager">记录当前测试工作单元操作的管理器。</param>
        public ScopedOperation(IUnitOfWorkScopeManager manager) => _manager = manager;

        /// <summary>
        /// 执行并完成工作单元作用域后返回其标识。
        /// </summary>
        /// <returns>本次操作使用的工作单元作用域标识。</returns>
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

using Bing.Tracing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Bing.Logging.BackgroundJobs.Tests;

/// <summary>
/// 后台任务队列测试
/// </summary>
public class BackgroundTaskQueueTest
{
    /// <summary>
    /// 测试目的：后台服务执行任务时应恢复入队时的日志上下文，并在任务完成后恢复父级状态。
    /// </summary>
    [Fact]
    public async Task QueueAsync_WhenWorkerExecutes_ShouldRestoreCapturedSnapshot()
    {
        // Arrange
        var services = CreateServices();
        await using var provider = services.BuildServiceProvider();
        var hostedService = provider.GetServices<IHostedService>().Single();
        await hostedService.StartAsync(CancellationToken.None);
        var queue = provider.GetRequiredService<IBackgroundTaskQueue>();
        var accessor = provider.GetRequiredService<ILogContextAccessor>();
        var completion = new TaskCompletionSource<LogContextSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var snapshot = new LogContextSnapshot(
            "queue-trace",
            new LogIdentityContext(userId: "queue-user"));

        try
        {
            // Act
            using (accessor.BeginScope(snapshot))
            {
                await queue.QueueAsync((serviceProvider, _) =>
                {
                    completion.TrySetResult(serviceProvider.GetRequiredService<ILogContextAccessor>().Capture());
                    return Task.CompletedTask;
                });
            }
            var completed = await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromSeconds(10)));

            // Assert
            completed.ShouldBe(completion.Task);
            var restored = await completion.Task;
            restored.TraceId.ShouldBe("queue-trace");
            restored.Identity.UserId.ShouldBe("queue-user");
            accessor.Current.TraceId.ShouldBeNull();
        }
        finally
        {
            await hostedService.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// 测试目的：单个任务抛出异常后，后台服务仍应继续消费后续任务。
    /// </summary>
    [Fact]
    public async Task QueueAsync_WhenTaskFails_ShouldContinueWithNextTask()
    {
        // Arrange
        var services = CreateServices();
        await using var provider = services.BuildServiceProvider();
        var hostedService = provider.GetServices<IHostedService>().Single();
        await hostedService.StartAsync(CancellationToken.None);
        var queue = provider.GetRequiredService<IBackgroundTaskQueue>();
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            // Act
            await queue.QueueAsync((_, _) => throw new InvalidOperationException("expected"));
            await queue.QueueAsync((_, _) =>
            {
                completion.TrySetResult(true);
                return Task.CompletedTask;
            });
            var completed = await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromSeconds(10)));

            // Assert
            completed.ShouldBe(completion.Task);
            (await completion.Task).ShouldBeTrue();
        }
        finally
        {
            await hostedService.StopAsync(CancellationToken.None);
        }
    }

    private static IServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ICorrelationIdProvider, DefaultCorrelationIdProvider>();
        services.AddSingleton<ICorrelationIdGenerator, DefaultCorrelationIdGenerator>();
        services.AddScoped<ILogContextAccessor, LogContextAccessor>();
        services.AddBingBackgroundTaskQueue(options => options.Capacity = 2);
        return services;
    }
}
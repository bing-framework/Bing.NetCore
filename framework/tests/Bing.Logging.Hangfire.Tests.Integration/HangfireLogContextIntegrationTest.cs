using Bing.Logging;
using Bing.Logging.Hangfire;
using Bing.Tracing;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bing.Logging.Hangfire.Tests.Integration;

/// <summary>
/// Hangfire日志上下文集成测试
/// </summary>
public class HangfireLogContextIntegrationTest
{
    /// <summary>
    /// 测试目的：真实Hangfire入队和执行链路应恢复创建任务时捕获的TraceId与用户上下文。
    /// </summary>
    [Fact]
    public async Task Enqueue_WhenWorkerExecutes_ShouldRestoreCapturedSnapshot()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ICorrelationIdProvider, DefaultCorrelationIdProvider>();
        services.AddSingleton<ICorrelationIdGenerator, DefaultCorrelationIdGenerator>();
        services.AddScoped<ILogContextAccessor, LogContextAccessor>();
        services.AddSingleton<JobRecorder>();
        services.AddTransient<TestJob>();
        using var provider = services.BuildServiceProvider();
        var storage = new MemoryStorage();
        GlobalConfiguration.Configuration
            .UseStorage(storage)
            .UseActivator(new ServiceProviderJobActivator(provider))
            .UseBingLogging(provider);
        using var server = new BackgroundJobServer(
            new BackgroundJobServerOptions { WorkerCount = 1 },
            storage);
        var accessor = provider.GetRequiredService<ILogContextAccessor>();
        var recorder = provider.GetRequiredService<JobRecorder>();
        var snapshot = new LogContextSnapshot(
            "hangfire-trace",
            new LogIdentityContext(userId: "hangfire-user"));

        // Act
        using (accessor.BeginScope(snapshot))
            BackgroundJob.Enqueue<TestJob>(job => job.Execute());
        var completed = await Task.WhenAny(recorder.Completion.Task, Task.Delay(TimeSpan.FromSeconds(15)));

        // Assert
        completed.ShouldBe(recorder.Completion.Task);
        var result = await recorder.Completion.Task;
        result.TraceId.ShouldBe("hangfire-trace");
        result.UserId.ShouldBe("hangfire-user");
    }

    public sealed class TestJob
    {
        private readonly ILogContextAccessor _accessor;
        private readonly JobRecorder _recorder;

        public TestJob(ILogContextAccessor accessor, JobRecorder recorder)
        {
            _accessor = accessor;
            _recorder = recorder;
        }

        public void Execute() => _recorder.Completion.TrySetResult(
            new JobResult(_accessor.Current.TraceId, _accessor.Current.Identity.UserId));
    }

    public sealed class JobRecorder
    {
        public TaskCompletionSource<JobResult> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public sealed class JobResult
    {
        public JobResult(string traceId, string userId)
        {
            TraceId = traceId;
            UserId = userId;
        }

        public string TraceId { get; }
        public string UserId { get; }
    }

    private sealed class ServiceProviderJobActivator : JobActivator
    {
        private readonly IServiceProvider _provider;

        public ServiceProviderJobActivator(IServiceProvider provider) => _provider = provider;

        public override JobActivatorScope BeginScope(JobActivatorContext context) =>
            new ServiceProviderJobActivatorScope(_provider.CreateScope());
    }

    private sealed class ServiceProviderJobActivatorScope : JobActivatorScope
    {
        private readonly IServiceScope _scope;

        public ServiceProviderJobActivatorScope(IServiceScope scope) => _scope = scope;

        public override object Resolve(Type type) => _scope.ServiceProvider.GetRequiredService(type);

        public override void DisposeScope() => _scope.Dispose();
    }
}
using Bing.Auditing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace Bing.Auditing.Tests;

/// <summary>
/// 验证审计作用域的异步流隔离与保存行为。
/// </summary>
public class AuditingManagerTest
{
    /// <summary>
    /// 验证审计属性设置器迁移到契约程序集后仍支持旧程序集限定名。
    /// </summary>
    [Fact]
    public void IAuditPropertySetter_LegacyAssemblyQualifiedName_ShouldResolve()
    {
        var resolved = Type.GetType("Bing.Auditing.IAuditPropertySetter, Bing.Auditing", throwOnError: true);

        resolved.ShouldBe(typeof(IAuditPropertySetter));
        resolved.Assembly.GetName().Name.ShouldBe("Bing.Auditing.Contracts");
    }

    /// <summary>
    /// 嵌套范围退出时恢复上层范围。
    /// </summary>
    [Fact]
    public void BeginScope_Nested_ShouldRestoreParent()
    {
        var manager = new AuditingManager(new RecordingStore());
        manager.Current.ShouldBeNull();
        using (manager.BeginScope())
        {
            var outer = manager.Current;
            using (manager.BeginScope())
                manager.Current.ShouldNotBeSameAs(outer);
            manager.Current.ShouldBeSameAs(outer);
        }
        manager.Current.ShouldBeNull();
    }

    /// <summary>
    /// 同一作用域并发保存只调用存储一次。
    /// </summary>
    [Fact]
    public async Task SaveAsync_ConcurrentCalls_ShouldStoreOnce()
    {
        var store = new RecordingStore();
        var manager = new AuditingManager(store);
        using var handle = manager.BeginScope();
        manager.Current.Log.UserId = "user-1";

        await Task.WhenAll(handle.SaveAsync(), handle.SaveAsync());

        store.Logs.Count.ShouldBe(1);
        store.Logs[0].UserId.ShouldBe("user-1");
    }

    /// <summary>
    /// 异步退出必须等待已经开始的审计保存。
    /// </summary>
    [Fact]
    public async Task BeginAsyncScope_InFlightSave_ShouldWaitBeforeDisposeCompletes()
    {
        var store = new DelayedStore();
        IAuditingManager manager = new AuditingManager(store);
        IAsyncAuditLogSaveHandle handle = manager.BeginAsyncScope();
        var save = handle.SaveAsync();
        var disposal = handle.DisposeAsync().AsTask();

        disposal.IsCompleted.ShouldBeFalse();
        manager.Current.ShouldBeNull();
        store.Complete();
        await Task.WhenAll(save, disposal);
        store.Calls.ShouldBe(1);
    }

    /// <summary>
    /// 保存失败在异步退出时传播，保存只启动一次。
    /// </summary>
    [Fact]
    public async Task BeginAsyncScope_SaveFailure_ShouldPropagateOnDispose()
    {
        var store = new DelayedStore();
        IAuditingManager manager = new AuditingManager(store);
        var handle = manager.BeginAsyncScope();
        var save = handle.SaveAsync();
        var failure = new InvalidOperationException("audit failed");
        var disposal = handle.DisposeAsync().AsTask();

        store.Fail(failure);

        (await Should.ThrowAsync<InvalidOperationException>(() => disposal)).ShouldBeSameAs(failure);
        (await Should.ThrowAsync<InvalidOperationException>(() => save)).ShouldBeSameAs(failure);
        store.Calls.ShouldBe(1);
    }

    /// <summary>
    /// 同步退出不能把未完成的异步保存报告为已清理。
    /// </summary>
    [Fact]
    public async Task BeginScope_InFlightSave_ShouldRequireAsyncDispose()
    {
        var store = new DelayedStore();
        IAuditingManager manager = new AuditingManager(store);
        var handle = manager.BeginScope();
        var save = handle.SaveAsync();

        Should.Throw<InvalidOperationException>(() => handle.Dispose());
        manager.Current.ShouldNotBeNull();
        var disposal = ((IAsyncDisposable)handle).DisposeAsync().AsTask();
        store.Complete();
        await Task.WhenAll(save, disposal);
        manager.Current.ShouldBeNull();
    }

    /// <summary>
    /// 并行执行流各自拥有审计日志。
    /// </summary>
    [Fact]
    public async Task BeginScope_ParallelFlows_ShouldNotShareLog()
    {
        var manager = new AuditingManager(new RecordingStore());
        var logs = await Task.WhenAll(Enumerable.Range(0, 2).Select(index => Task.Run(() =>
        {
            using var handle = manager.BeginScope();
            manager.Current.Log.UserId = $"user-{index}";
            return manager.Current.Log;
        })));

        logs[0].ShouldNotBeSameAs(logs[1]);
        manager.Current.ShouldBeNull();
    }

    /// <summary>
    /// 注册入口尊重调用方的持久化存储。
    /// </summary>
    [Fact]
    public void AddBingAuditing_ShouldKeepCustomStore()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuditingStore, RecordingStore>();
        services.AddBingAuditing();
        services.AddBingAuditing();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IAuditingStore>().ShouldBeOfType<RecordingStore>();
        scope.ServiceProvider.GetRequiredService<IAuditingManager>().ShouldBeSameAs(
            scope.ServiceProvider.GetRequiredService<IAuditingManager>());
    }

    /// <summary>
    /// 默认存储使用 DI 创建的日志记录器。
    /// </summary>
    [Fact]
    public void AddBingAuditing_DefaultStore_ShouldResolveLogger()
    {
        var services = new ServiceCollection();
        services.AddBingAuditing();
        using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredService<IAuditingStore>().ShouldBeOfType<SimpleLogAuditingStore>();
        store.Logger.ShouldNotBeNull();
        store.Logger.ShouldNotBeOfType<Microsoft.Extensions.Logging.Abstractions.NullLogger<SimpleLogAuditingStore>>();
    }

    /// <summary>
    /// 默认日志不输出审计参数及异常内容。
    /// </summary>
    [Fact]
    public async Task DefaultStore_ShouldNotLogSensitiveContents()
    {
        var logger = new Mock<ILogger<SimpleLogAuditingStore>>();
        var messages = new List<string>();
        logger.Setup(x => x.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception, string>>()))
            .Callback(new InvocationAction(invocation => messages.Add(invocation.Arguments[2].ToString())));
        var store = new SimpleLogAuditingStore(logger.Object);
        var audit = new AuditLogInfo { CorrelationId = "trace-1", Url = "/orders?token=secret-token" };
        audit.Exceptions.Add(new InvalidOperationException("secret-exception"));

        await store.SaveAsync(audit);

        messages.Count.ShouldBe(1);
        messages[0].ShouldNotContain("secret-token");
        messages[0].ShouldNotContain("secret-exception");
        messages[0].ShouldContain("trace-1");
    }

    /// <summary>
    /// 保存接收到的审计日志。
    /// </summary>
    private sealed class RecordingStore : IAuditingStore
    {
        /// <summary>
        /// 保存的日志。
        /// </summary>
        public List<AuditLogInfo> Logs { get; } = new List<AuditLogInfo>();

        /// <inheritdoc />
        public Task SaveAsync(AuditLogInfo auditInfo)
        {
            lock (Logs)
                Logs.Add(auditInfo);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 允许测试控制异步审计保存完成时机。
    /// </summary>
    private sealed class DelayedStore : IAuditingStore
    {
        /// <summary>
        /// 保存完成信号。
        /// </summary>
        private readonly TaskCompletionSource<bool> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 保存调用次数。
        /// </summary>
        public int Calls { get; private set; }

        /// <inheritdoc />
        public Task SaveAsync(AuditLogInfo auditInfo)
        {
            Calls++;
            return _completion.Task;
        }

        /// <summary>
        /// 完成保存。
        /// </summary>
        public void Complete() => _completion.SetResult(true);

        /// <summary>
        /// 让保存失败。
        /// </summary>
        public void Fail(Exception exception) => _completion.SetException(exception);
    }
}

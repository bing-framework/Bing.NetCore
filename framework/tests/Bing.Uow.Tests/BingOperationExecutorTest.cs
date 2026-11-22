using Bing.Uow;
using Bing.Tracing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bing.Uow.Tests;

/// <summary>
/// 验证业务操作执行器的 DI scope、解析顺序、取消与清理协议。
/// </summary>
public sealed class BingOperationExecutorTest
{
    /// <summary>
    /// 验证执行器在新作用域开始工作单元后解析目标服务。
    /// </summary>
    [Fact]
    public async Task Execute_ShouldResolveServiceAfterBeginInNewScope()
    {
        var services = new ServiceCollection();
        services.AddBingOperations();
        services.AddScoped<OperationProbe>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var callerScope = provider.CreateScope();
        var callerManager = callerScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();
        var callerRoot = callerManager.Begin();
        var executor = provider.GetRequiredService<IBingOperationExecutor>();

        var result = await executor.ExecuteAsync<OperationProbe, Guid>((service, _) =>
        {
            service.Root.ShouldNotBeNull();
            service.Root.ShouldNotBeSameAs(callerRoot);
            service.Manager.Current.ShouldBeSameAs(service.Root);
            return Task.FromResult(service.ScopeId);
        });

        result.ShouldNotBe(Guid.Empty);
        callerManager.Current.ShouldBeSameAs(callerRoot);
        callerRoot.Rollback();
        callerRoot.Dispose();
    }

    /// <summary>
    /// 验证预先取消的操作不解析目标服务。
    /// </summary>
    [Fact]
    public async Task PreCancellation_ShouldNotResolveTarget()
    {
        var resolveCount = 0;
        var services = new ServiceCollection();
        services.AddBingOperations();
        services.AddScoped<OperationProbe>(_ =>
        {
            resolveCount++;
            return null;
        });
        using var provider = services.BuildServiceProvider();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            provider.GetRequiredService<IBingOperationExecutor>().ExecuteAsync<OperationProbe>(
                (_, _) => Task.CompletedTask, cancellationToken: cancellation.Token));

        resolveCount.ShouldBe(0);
    }

    /// <summary>
    /// 验证解析失败时先释放根工作单元再释放新作用域。
    /// </summary>
    [Fact]
    public async Task ResolutionFailure_ShouldDisposeNewScopeAfterRoot()
    {
        var services = new ServiceCollection();
        services.AddBingOperations();
        services.AddScoped<DisposeProbe>();
        services.AddScoped<BrokenService>(provider =>
        {
            provider.GetRequiredService<DisposeProbe>();
            throw new InvalidOperationException("service construction failed");
        });
        using var provider = services.BuildServiceProvider();

        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            provider.GetRequiredService<IBingOperationExecutor>().ExecuteAsync<BrokenService>(
                (_, _) => Task.CompletedTask));

        exception.Message.ShouldBe("service construction failed");
    }

    /// <summary>
    /// 验证并行操作使用独立作用域与根工作单元。
    /// </summary>
    [Fact]
    public async Task ParallelOperations_ShouldUseIndependentScopesAndRoots()
    {
        var services = new ServiceCollection();
        services.AddBingOperations();
        services.AddScoped<OperationProbe>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var executor = provider.GetRequiredService<IBingOperationExecutor>();

        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            executor.ExecuteAsync<OperationProbe, Guid>(async (service, _) =>
            {
                var root = service.Manager.Current;
                root.ShouldBeSameAs(service.Root);
                await Task.Delay(20);
                return root.Id;
            })));

        results.Distinct().Count().ShouldBe(2);
    }

    /// <summary>
    /// 验证构造前复制追踪上下文并在嵌套及并行操作后恢复。
    /// </summary>
    [Fact]
    public async Task Execute_ShouldCopyTraceBeforeConstructionAndRestoreAfterNestedAndParallelWork()
    {
        var services = new ServiceCollection();
        services.AddBingOperations();
        services.AddScoped<TraceProbe>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var executor = provider.GetRequiredService<IBingOperationExecutor>();
        var previous = TraceIdContext.Current;
        var parent = new TraceIdContext("trace-parent", "root-parent", "parent-parent", "child-parent");
        TraceIdContext.Current = parent;
        TraceProbe nestedProbe = null;
        try
        {
            await executor.ExecuteAsync<TraceProbe>(async (outerProbe, _) =>
            {
                var outerTrace = TraceIdContext.Current;
                outerProbe.Trace.ShouldBeSameAs(outerTrace);
                outerProbe.Trace.ShouldNotBeSameAs(parent);
                outerProbe.Trace.TraceId.ShouldBe(parent.TraceId);
                outerProbe.Trace.RootId.ShouldBe(parent.RootId);
                outerProbe.Trace.ParentId.ShouldBe(parent.ParentId);
                outerProbe.Trace.ChildId.ShouldBe(parent.ChildId);

                await executor.ExecuteAsync<TraceProbe>((innerProbe, _) =>
                {
                    nestedProbe = innerProbe;
                    innerProbe.Trace.ShouldNotBeSameAs(outerTrace);
                    innerProbe.Trace.TraceId.ShouldBe(parent.TraceId);
                    innerProbe.Trace.RootId.ShouldBe(parent.RootId);
                    innerProbe.Trace.ParentId.ShouldBe(parent.ParentId);
                    innerProbe.Trace.ChildId.ShouldBe(parent.ChildId);
                    innerProbe.Trace.TraceId = "nested-mutated";
                    return Task.CompletedTask;
                });

                TraceIdContext.Current.ShouldBeSameAs(outerTrace);
                outerTrace.TraceId.ShouldBe(parent.TraceId);
                outerTrace.RootId.ShouldBe(parent.RootId);
                outerTrace.ParentId.ShouldBe(parent.ParentId);
                outerTrace.ChildId.ShouldBe(parent.ChildId);
            });

            var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var bothEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var parallelTraces = new System.Collections.Concurrent.ConcurrentBag<TraceIdContext>();
            var entered = 0;
            Task RunParallelAsync() => executor.ExecuteAsync<TraceProbe>(async (probe, token) =>
            {
                parallelTraces.Add(probe.Trace);
                probe.Trace.TraceId.ShouldBe(parent.TraceId);
                if (Interlocked.Increment(ref entered) == 2)
                    bothEntered.TrySetResult();
                else
                    firstEntered.TrySetResult();
                await release.Task.WaitAsync(token);
                probe.Trace.TraceId = "parallel-mutated";
                return;
            });

            var first = RunParallelAsync();
            await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var second = RunParallelAsync();
            await bothEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            parallelTraces.Count.ShouldBe(2);
            parallelTraces.Distinct(ReferenceEqualityComparer.Instance).Count().ShouldBe(2);
            parallelTraces.ShouldNotContain(parent);
            release.TrySetResult();
            await Task.WhenAll(first, second);

            parallelTraces.ShouldAllBe(trace => trace.TraceId == "parallel-mutated");
            parent.TraceId.ShouldBe("trace-parent");
            parent.RootId.ShouldBe("root-parent");
            parent.ParentId.ShouldBe("parent-parent");
            parent.ChildId.ShouldBe("child-parent");
            TraceIdContext.Current.ShouldBeSameAs(parent);
            nestedProbe.Trace.TraceId.ShouldBe("nested-mutated");
        }
        finally
        {
            TraceIdContext.Current = previous;
        }
    }

    /// <summary>
    /// 验证解析、业务、取消及清理失败后均恢复父追踪上下文。
    /// </summary>
    [Fact]
    public async Task Execute_ShouldRestoreParentTraceAfterResolutionBusinessCancellationAndCleanupFailures()
    {
        var parent = new TraceIdContext("trace-failure", "root-failure", "parent-failure", "child-failure");
        var previous = TraceIdContext.Current;
        TraceIdContext.Current = parent;
        try
        {
            var observedDuringResolution = (TraceIdContext)null;
            var resolutionServices = new ServiceCollection();
            resolutionServices.AddBingOperations();
            resolutionServices.AddScoped<BrokenTraceProbe>(_ =>
            {
                observedDuringResolution = TraceIdContext.Current;
                throw new InvalidOperationException("trace resolution failed");
            });
            using (var provider = resolutionServices.BuildServiceProvider())
            {
                var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
                    provider.GetRequiredService<IBingOperationExecutor>().ExecuteAsync<BrokenTraceProbe>(
                        (_, _) => Task.CompletedTask));
                exception.Message.ShouldBe("trace resolution failed");
            }
            observedDuringResolution.ShouldNotBeSameAs(parent);
            observedDuringResolution.TraceId.ShouldBe(parent.TraceId);
            TraceIdContext.Current.ShouldBeSameAs(parent);

            var services = new ServiceCollection();
            services.AddBingOperations();
            services.AddScoped<TraceProbe>();
            services.AddScoped<TraceDisposeProbe>();
            using var providerWithServices = services.BuildServiceProvider();
            var executor = providerWithServices.GetRequiredService<IBingOperationExecutor>();

            var businessFailure = await Should.ThrowAsync<InvalidOperationException>(() =>
                executor.ExecuteAsync<TraceProbe>((probe, _) =>
                {
                    probe.Trace.TraceId = "business-failure";
                    throw new InvalidOperationException("trace business failed");
                }));
            businessFailure.Message.ShouldBe("trace business failed");
            TraceIdContext.Current.ShouldBeSameAs(parent);

            using var cancellation = new CancellationTokenSource();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var cancelled = executor.ExecuteAsync<TraceProbe>(async (_, token) =>
            {
                entered.TrySetResult();
                await Task.Delay(Timeout.Infinite, token);
            }, cancellationToken: cancellation.Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();
            await Should.ThrowAsync<OperationCanceledException>(() => cancelled);
            TraceIdContext.Current.ShouldBeSameAs(parent);

            var cleanupFailure = await Should.ThrowAsync<InvalidOperationException>(() =>
                executor.ExecuteAsync<TraceDisposeProbe>((_, _) => Task.CompletedTask));
            cleanupFailure.Message.ShouldBe("trace cleanup failed");
            TraceIdContext.Current.ShouldBeSameAs(parent);
            parent.TraceId.ShouldBe("trace-failure");
            parent.RootId.ShouldBe("root-failure");
            parent.ParentId.ShouldBe("parent-failure");
            parent.ChildId.ShouldBe("child-failure");
        }
        finally
        {
            TraceIdContext.Current = previous;
        }
    }

    /// <summary>
    /// 保存BingOperationExecutorTest测试关注的执行观测结果。
    /// </summary>
    private sealed class OperationProbe
    {
        /// <summary>
        /// 初始化一个 <see cref="OperationProbe" /> 类型的实例。
        /// </summary>
        /// <param name="manager">记录当前测试工作单元操作的管理器。</param>
        public OperationProbe(IUnitOfWorkScopeManager manager)
        {
            Manager = manager;
            Root = manager.Current ?? throw new InvalidOperationException(
                "执行器必须在解析业务服务前开始工作单元。");
            ScopeId = Guid.NewGuid();
        }

        /// <summary>
        /// 获取测试使用的工作单元管理器。
        /// </summary>
        public IUnitOfWorkScopeManager Manager { get; }
        /// <summary>
        /// 获取当前测试对应的根作用域。
        /// </summary>
        public IUnitOfWorkScope Root { get; }
        /// <summary>
        /// 获取当前操作作用域的唯一标识。
        /// </summary>
        public Guid ScopeId { get; }
    }

    /// <summary>
    /// 在工作单元执行测试中模拟服务调用失败。
    /// </summary>
    private sealed class BrokenService
    {
    }

    /// <summary>
    /// 保存BingOperationExecutorTest测试关注的执行观测结果。
    /// </summary>
    private sealed class DisposeProbe : IDisposable
    {
        /// <summary>
        /// 保存释放探针使用的工作单元作用域管理器。
        /// </summary>
        private readonly IUnitOfWorkScopeManager _manager;

        /// <summary>
        /// 初始化一个 <see cref="DisposeProbe" /> 类型的实例。
        /// </summary>
        /// <param name="manager">记录当前测试工作单元操作的管理器。</param>
        public DisposeProbe(IUnitOfWorkScopeManager manager) => _manager = manager;

        /// <inheritdoc />
        public void Dispose() => _manager.Current.ShouldBeNull();
    }

    /// <summary>
    /// 保存BingOperationExecutorTest测试关注的执行观测结果。
    /// </summary>
    private sealed class TraceProbe
    {
        /// <summary>
        /// 初始化一个 <see cref="TraceProbe" /> 类型的实例。
        /// </summary>
        /// <param name="manager">记录当前测试工作单元操作的管理器。</param>
        public TraceProbe(IUnitOfWorkScopeManager manager)
        {
            manager.Current.ShouldNotBeNull();
            Trace = TraceIdContext.Current ?? throw new InvalidOperationException(
                "Trace must be established before resolving the operation service.");
        }

        /// <summary>
        /// 获取测试使用的调用跟踪记录。
        /// </summary>
        public TraceIdContext Trace { get; }
    }

    /// <summary>
    /// 保存BingOperationExecutorTest测试关注的执行观测结果。
    /// </summary>
    private sealed class BrokenTraceProbe
    {
    }

    /// <summary>
    /// 保存BingOperationExecutorTest测试关注的执行观测结果。
    /// </summary>
    private sealed class TraceDisposeProbe : IDisposable
    {
        /// <summary>
        /// 初始化一个 <see cref="TraceDisposeProbe" /> 类型的实例。
        /// </summary>
        public TraceDisposeProbe() => TraceIdContext.Current.ShouldNotBeNull();

        /// <inheritdoc />
        public void Dispose() => throw new InvalidOperationException("trace cleanup failed");
    }
}

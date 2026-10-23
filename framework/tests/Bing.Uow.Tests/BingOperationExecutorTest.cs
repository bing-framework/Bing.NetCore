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

    private sealed class OperationProbe
    {
        public OperationProbe(IUnitOfWorkScopeManager manager)
        {
            Manager = manager;
            Root = manager.Current ?? throw new InvalidOperationException(
                "执行器必须在解析业务服务前开始工作单元。");
            ScopeId = Guid.NewGuid();
        }

        public IUnitOfWorkScopeManager Manager { get; }
        public IUnitOfWorkScope Root { get; }
        public Guid ScopeId { get; }
    }

    private sealed class BrokenService
    {
    }

    private sealed class DisposeProbe : IDisposable
    {
        private readonly IUnitOfWorkScopeManager _manager;

        public DisposeProbe(IUnitOfWorkScopeManager manager) => _manager = manager;

        public void Dispose() => _manager.Current.ShouldBeNull();
    }

    private sealed class TraceProbe
    {
        public TraceProbe(IUnitOfWorkScopeManager manager)
        {
            manager.Current.ShouldNotBeNull();
            Trace = TraceIdContext.Current ?? throw new InvalidOperationException(
                "Trace must be established before resolving the operation service.");
        }

        public TraceIdContext Trace { get; }
    }

    private sealed class BrokenTraceProbe
    {
    }

    private sealed class TraceDisposeProbe : IDisposable
    {
        public TraceDisposeProbe() => TraceIdContext.Current.ShouldNotBeNull();

        public void Dispose() => throw new InvalidOperationException("trace cleanup failed");
    }
}

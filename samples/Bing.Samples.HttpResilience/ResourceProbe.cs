using Bing.Resilience;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Bing.Http.Resilience;

/// <summary>有界的成功路径分配、延迟与宿主生命周期探针，不作为生产容量阈值。</summary>
internal static class ResourceProbe
{
    internal static async Task RunAsync()
    {
        const int operations = 2000;
        var observations = new List<object>();
        foreach (var enabled in new[] { false, true })
        {
            var services = new ServiceCollection();
        services.AddBingResilience().AddFencesProvider("primary").UseDefaultProvider("primary");
            var builder = services.AddHttpClient("probe").ConfigurePrimaryHttpMessageHandler(() => new SuccessHandler());
            if (enabled) builder.AddBingHttpResilience("probe");
            await using var provider = services.BuildServiceProvider();
            using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("probe");
            for (int i = 0; i < 100; i++) using (await client.GetAsync("http://local.test")) { }
            var allocated = GC.GetTotalAllocatedBytes(true);
            var timer = Stopwatch.StartNew();
            for (int i = 0; i < operations; i++) using (await client.GetAsync("http://local.test")) { }
            timer.Stop();
            observations.Add(new { enabled, operations, elapsedMs = timer.Elapsed.TotalMilliseconds,
                allocatedBytes = GC.GetTotalAllocatedBytes(true) - allocated,
                requestedConcurrency = 1, maxActualParallelism = 1, maxQueuedRequests = 0 });
        }

        // 同时观察无恢复基线与启用恢复的宿主，避免将工厂自身的延迟回收误归因于恢复层。
        var groups = new[] { new LifetimeObservation(false), new LifetimeObservation(true) };
        foreach (var group in groups)
            for (int i = 0; i < 20; i++) CreateAndDisposeProvider(group);
        var immediatelyAfterDispose = groups.Select(g => g.Snapshot()).ToArray();
        var elapsed = Stopwatch.StartNew();
        var deadline = TimeSpan.FromSeconds(20);
        do
        {
            // 仅测试探针强制 GC，让工厂的弱引用清理条件成立；不在生产调用中执行。
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            if (groups.All(g => g.IsReclaimed)) break;
            await Task.Delay(250);
        } while (elapsed.Elapsed < deadline);

        var passed = groups.All(g => g.IsReclaimed);
        Console.WriteLine(JsonSerializer.Serialize(new { runtime = Environment.Version.ToString(), observations,
            lifecycle = new { handlerLifetimeMs = 1000, observationBudgetMs = deadline.TotalMilliseconds,
                elapsedMs = elapsed.Elapsed.TotalMilliseconds, immediatelyAfterDispose,
                afterObservation = groups.Select(g => g.Snapshot()).ToArray(),
                status = passed ? "PASS" : "NOT_VERIFIED" } }, new JsonSerializerOptions { WriteIndented = true }));
        if (!passed)
            throw new InvalidOperationException("有界生命周期观察未确认全部资源回收；不能据此宣称生命周期 PASS。");
    }

    // 单独的非内联方法确保离开创建帧后，不由 JIT 局部变量延长 client/provider/handler 寿命。
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CreateAndDisposeProvider(LifetimeObservation observation)
    {
        var services = new ServiceCollection();
        services.AddBingResilience().AddFencesProvider("primary").UseDefaultProvider("primary");
        var builder = services.AddHttpClient("lifecycle")
            .SetHandlerLifetime(TimeSpan.FromSeconds(1))
            .ConfigurePrimaryHttpMessageHandler(() => new SuccessHandler(observation));
        if (observation.Enabled) builder.AddBingHttpResilience("lifecycle");
        builder.ConfigureAdditionalHttpMessageHandlers((handlers, _) =>
        {
            foreach (var handler in handlers) observation.Handlers.Add(new WeakReference(handler));
        });
        var provider = services.BuildServiceProvider();
        observation.Providers.Add(new WeakReference(provider));
        try
        {
            using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("lifecycle");
            observation.Clients.Add(new WeakReference(client));
            using var result = client.GetAsync("http://local.test").GetAwaiter().GetResult();
        }
        finally
        {
            provider.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private sealed class LifetimeObservation(bool enabled)
    {
        internal bool Enabled { get; } = enabled;
        internal int Created;
        internal int Disposed;
        internal List<WeakReference> Providers { get; } = new();
        internal List<WeakReference> Clients { get; } = new();
        internal List<WeakReference> Handlers { get; } = new();
        internal bool IsReclaimed => Volatile.Read(ref Disposed) == Created &&
            !Providers.Concat(Clients).Concat(Handlers).Any(r => r.IsAlive);
        internal object Snapshot() => new { enabled = Enabled, providersCreated = Providers.Count,
            transportsCreated = Created, transportsDisposed = Volatile.Read(ref Disposed),
            retainedProviders = Providers.Count(r => r.IsAlive), retainedClients = Clients.Count(r => r.IsAlive),
            handlersObserved = Handlers.Count, retainedHandlers = Handlers.Count(r => r.IsAlive) };
    }

    private sealed class SuccessHandler : HttpMessageHandler
    {
        private readonly LifetimeObservation? _observation;
        private int _disposed;
        internal SuccessHandler(LifetimeObservation? observation = null)
        {
            _observation = observation;
            if (observation != null)
            {
                Interlocked.Increment(ref observation.Created);
                observation.Handlers.Add(new WeakReference(this));
            }
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        protected override void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0 && _observation != null)
                Interlocked.Increment(ref _observation.Disposed);
            base.Dispose(disposing);
        }
    }
}

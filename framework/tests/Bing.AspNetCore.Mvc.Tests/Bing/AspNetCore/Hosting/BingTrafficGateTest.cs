using Bing.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Bing.AspNetCore.Mvc.Tests.Bing.AspNetCore.Hosting;

/// <summary>
/// 验证 HTTP 流量闸门的原子计数和排空语义。
/// </summary>
public class BingTrafficGateTest
{
    /// <summary>
    /// 重复释放请求句柄不会减少两次在途计数。
    /// </summary>
    [Fact]
    public void TryEnter_WhenLeaseDisposedTwice_ShouldReleaseOnce()
    {
        var gate = new BingTrafficGate();
        gate.TryEnter(out var lease).ShouldBeTrue();
        gate.InFlightRequests.ShouldBe(1);

        lease.Dispose();
        lease.Dispose();

        gate.InFlightRequests.ShouldBe(0);
    }

    /// <summary>
    /// 关闭启动即就绪时，标记前拒绝请求并返回未就绪状态。
    /// </summary>
    [Fact]
    public void StartReadyDisabled_ShouldRequireExplicitReadyMark()
    {
        var gate = new BingTrafficGate(new BingTrafficGateOptions { StartReady = false });

        gate.IsReady.ShouldBeFalse();
        gate.TryEnter(out _).ShouldBeFalse();
        gate.MarkReady();
        gate.IsReady.ShouldBeTrue();
        gate.TryEnter(out var lease).ShouldBeTrue();
        lease.Dispose();
    }

    /// <summary>
    /// 排空后拒绝新请求，并在已有请求释放后完成等待。
    /// </summary>
    [Fact]
    public async Task BeginDrain_ShouldRejectNewRequestAndWaitForExisting()
    {
        var gate = new BingTrafficGate();
        gate.TryEnter(out var lease).ShouldBeTrue();
        gate.BeginDrain();

        gate.IsDraining.ShouldBeTrue();
        gate.IsReady.ShouldBeFalse();
        gate.TryEnter(out _).ShouldBeFalse();
        (await gate.WaitForDrainAsync(TimeSpan.FromMilliseconds(20))).ShouldBeFalse();

        lease.Dispose();

        (await gate.WaitForDrainAsync(TimeSpan.FromSeconds(1))).ShouldBeTrue();
    }

    /// <summary>
    /// 取消等待只结束等待，不将未释放请求误报为已排空。
    /// </summary>
    [Fact]
    public async Task WaitForDrainAsync_WhenCancelled_ShouldReturnFalse()
    {
        var gate = new BingTrafficGate();
        gate.TryEnter(out var lease).ShouldBeTrue();
        gate.BeginDrain();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        (await gate.WaitForDrainAsync(TimeSpan.FromMinutes(1), cancellation.Token)).ShouldBeFalse();
        gate.InFlightRequests.ShouldBe(1);
        lease.Dispose();
    }

    /// <summary>
    /// 中间件提供就绪探针并在排空时返回 503。
    /// </summary>
    [Fact]
    public async Task Middleware_ShouldExposeReadinessAndRejectDrainingRequest()
    {
        var gate = new BingTrafficGate();
        var forwarded = false;
        var middleware = new BingTrafficGateMiddleware(_ =>
        {
            forwarded = true;
            return Task.CompletedTask;
        }, gate, Microsoft.Extensions.Options.Options.Create(new BingTrafficGateOptions()));

        var readiness = new DefaultHttpContext();
        readiness.Request.Path = "/health/ready";
        await middleware.InvokeAsync(readiness);
        readiness.Response.StatusCode.ShouldBe(200);

        gate.BeginDrain();
        var request = new DefaultHttpContext();
        await middleware.InvokeAsync(request);

        forwarded.ShouldBeFalse();
        request.Response.StatusCode.ShouldBe(503);
        request.Response.Headers["Retry-After"].ToString().ShouldBe("1");
    }

    /// <summary>
    /// 注册入口保留调用方配置并只注册一个流量闸门。
    /// </summary>
    [Fact]
    public void AddBingTrafficGate_ShouldRegisterConfiguredSingleton()
    {
        var services = new ServiceCollection();
        services.AddBingTrafficGate(options => options.DrainTimeout = TimeSpan.FromSeconds(5));
        services.AddBingTrafficGate(options => options.DrainTimeout = TimeSpan.FromSeconds(5));
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IBingTrafficGate>().ShouldBeSameAs(
            provider.GetRequiredService<IBingTrafficGate>());
        provider.GetRequiredService<IOptions<BingTrafficGateOptions>>().Value.DrainTimeout
            .ShouldBe(TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// 宿主停止取消等待时不强行释放仍在执行的请求。
    /// </summary>
    [Fact]
    public async Task HostedService_WhenStopWaitIsCancelled_ShouldKeepActiveRequest()
    {
        var services = new ServiceCollection();
        services.AddBingTrafficGate(options => options.DrainTimeout = TimeSpan.FromMinutes(1));
        using var provider = services.BuildServiceProvider();
        var gate = provider.GetRequiredService<IBingTrafficGate>();
        gate.TryEnter(out var lease).ShouldBeTrue();
        var hosted = provider.GetServices<IHostedService>()
            .Single(service => service.GetType().Name == "BingTrafficGateHostedService");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));

        var exception = await Should.ThrowAsync<OperationCanceledException>(() => hosted.StopAsync(cancellation.Token));

        exception.CancellationToken.ShouldBe(cancellation.Token);
        gate.InFlightRequests.ShouldBe(1);
        lease.Dispose();
        (await gate.WaitForDrainAsync(TimeSpan.FromSeconds(1))).ShouldBeTrue();
    }

    /// <summary>
    /// 排空超时通过异常和在途计数报告未完成。
    /// </summary>
    [Fact]
    public async Task HostedService_WhenDrainTimesOut_ShouldReportIncompleteDrain()
    {
        var services = new ServiceCollection();
        services.AddBingTrafficGate(options => options.DrainTimeout = TimeSpan.FromMilliseconds(20));
        using var provider = services.BuildServiceProvider();
        var gate = provider.GetRequiredService<IBingTrafficGate>();
        gate.TryEnter(out var lease).ShouldBeTrue();
        var hosted = provider.GetServices<IHostedService>()
            .Single(service => service.GetType().Name == "BingTrafficGateHostedService");

        var exception = await Should.ThrowAsync<TimeoutException>(() => hosted.StopAsync(CancellationToken.None));

        exception.Data["Bing.TrafficGateInFlight"].ShouldBe(1);
        gate.IsDraining.ShouldBeTrue();
        gate.InFlightRequests.ShouldBe(1);
        lease.Dispose();
        await hosted.StopAsync(CancellationToken.None);
    }

    /// <summary>
    /// 并发准入与排空后所有计数归零且不能重新进入接收状态。
    /// </summary>
    [Fact]
    public async Task BeginDrain_WhenEntriesRace_ShouldRemainTerminalAndReleaseEveryLease()
    {
        var gate = new BingTrafficGate();
        gate.TryEnter(out var initial).ShouldBeTrue();
        var leases = new System.Collections.Concurrent.ConcurrentBag<IDisposable>();
        var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
        {
            if (gate.TryEnter(out var lease))
                leases.Add(lease);
        })).ToArray();
        gate.BeginDrain();
        await Task.WhenAll(tasks);
        gate.MarkReady();

        gate.IsReady.ShouldBeFalse();
        gate.TryEnter(out _).ShouldBeFalse();
        foreach (var lease in leases)
            lease.Dispose();
        initial.Dispose();

        (await gate.WaitForDrainAsync(TimeSpan.FromSeconds(1))).ShouldBeTrue();
        gate.InFlightRequests.ShouldBe(0);
    }
}

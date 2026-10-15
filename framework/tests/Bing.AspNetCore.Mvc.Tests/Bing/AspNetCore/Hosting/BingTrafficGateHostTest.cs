using Bing.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bing.AspNetCore.Mvc.Tests.Bing.AspNetCore.Hosting;

/// <summary>
/// 验证宿主停止期间的真实请求排空。
/// </summary>
public class BingTrafficGateHostTest
{
    /// <summary>
    /// 超时停止报告未完成，并保留活动请求的作用域资源。
    /// </summary>
    [Fact]
    public async Task HostStop_WhenRequestIsActive_ShouldReportTimeoutAndKeepRequestResource()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        RequestResource resource = null;
        using var host = new HostBuilder().ConfigureWebHost(web => web.UseTestServer()
            .ConfigureServices(services =>
            {
                services.AddScoped<RequestResource>();
                services.AddBingTrafficGate(options => options.DrainTimeout = TimeSpan.FromMilliseconds(20));
            })
            .Configure(app =>
            {
                app.UseBingTrafficGate();
                app.Run(async context =>
                {
                    resource = context.RequestServices.GetRequiredService<RequestResource>();
                    entered.TrySetResult(true);
                    await release.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    resource.DisposeCount.ShouldBe(0);
                    await context.Response.WriteAsync("completed");
                });
            })).Build();
        await host.StartAsync();
        using var client = host.GetTestClient();
        var request = client.GetAsync("/long-request");
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var exception = await Record.ExceptionAsync(() => host.StopAsync());
            // Generic Host 在 net6 聚合单个停止错误，在 net8 直接传播该错误。
            var timeout = exception is AggregateException aggregate
                ? aggregate.InnerExceptions.ShouldHaveSingleItem().ShouldBeOfType<TimeoutException>()
                : exception.ShouldBeOfType<TimeoutException>();
            timeout.Data["Bing.TrafficGateInFlight"].ShouldBe(1);
            resource.DisposeCount.ShouldBe(0);
            host.Services.GetRequiredService<IBingTrafficGate>().IsReady.ShouldBeFalse();
        }
        finally
        {
            release.TrySetResult(true);
            using var response = await request;
            response.EnsureSuccessStatusCode();
        }
        var gate = host.Services.GetRequiredService<IBingTrafficGate>();
        (await gate.WaitForDrainAsync(TimeSpan.FromSeconds(5))).ShouldBeTrue();
        await resource.WhenDisposed.WaitAsync(TimeSpan.FromSeconds(5));
        resource.DisposeCount.ShouldBe(1);
    }

    /// <summary>
    /// 正常停止等待实际请求结束后返回。
    /// </summary>
    [Fact]
    public async Task HostStop_ShouldWaitForRequestCompletion()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var host = new HostBuilder().ConfigureWebHost(web => web.UseTestServer()
            .ConfigureServices(services => services.AddBingTrafficGate(options => options.DrainTimeout = TimeSpan.FromSeconds(5)))
            .Configure(app =>
            {
                app.UseBingTrafficGate();
                app.Run(async context =>
                {
                    entered.TrySetResult(true);
                    await release.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    await context.Response.WriteAsync("completed");
                });
            })).Build();
        await host.StartAsync();
        using var client = host.GetTestClient();
        var request = client.GetAsync("/long-request");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var stopping = host.StopAsync();
        try
        {
            stopping.IsCompleted.ShouldBeFalse();
        }
        finally
        {
            release.TrySetResult(true);
        }
        using var response = await request;
        response.EnsureSuccessStatusCode();
        await stopping;
        host.Services.GetRequiredService<IBingTrafficGate>().InFlightRequests.ShouldBe(0);
    }

    /// <summary>
    /// 记录请求作用域的资源释放次数。
    /// </summary>
    private sealed class RequestResource : IDisposable
    {
        /// <summary>
        /// 释放次数。
        /// </summary>
        private int _disposed;

        /// <summary>
        /// 请求作用域实际释放完成通知。
        /// </summary>
        private readonly TaskCompletionSource<bool> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 获取资源释放次数。
        /// </summary>
        public int DisposeCount => Volatile.Read(ref _disposed);

        /// <summary>
        /// 获取请求作用域的释放通知。
        /// </summary>
        public Task WhenDisposed => _completion.Task;

        /// <inheritdoc />
        public void Dispose()
        {
            Interlocked.Increment(ref _disposed);
            _completion.TrySetResult(true);
        }
    }
}

using System.Net;
using System.Net.Sockets;
using System.Text;
using Bing.DynamicApi.WebApiClient;
using Bing.Http.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.DynamicApi.Client.Resilience.Tests;

public sealed class LoopbackTests
{
    [Fact]
    public async Task RealHttpSuccessfulBodyStallIsCancelledByCallerDeadline()
    {
        await using var server = new LoopbackServer(async (client, ct) =>
        {
            var stream = client.GetStream();
            await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 20\r\nConnection: close\r\n\r\n"), ct);
            await stream.FlushAsync(ct);
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        });
        var services = new ServiceCollection();
        services.AddBingResilience().AddFencesProvider("test").UseDefaultProvider("test");
        services.AddBingWebApiClient<IResilientSdk>(o => o.BaseAddress = new Uri(server.Url))
            .AddBingHttpResilience("loopback", o => o.CanRetryRequest = _ => true);
        using var provider = services.BuildServiceProvider();
        using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => provider.GetRequiredService<IResilientSdk>().ReadAsync(deadline.Token));
        Assert.IsAssignableFrom<OperationCanceledException>(error.InnerException);
        Assert.Equal(1, server.Connections);
    }

    [Fact]
    public async Task RealConnectionResetSurfacesNetworkFailureWithinBudget()
    {
        await using var server = new LoopbackServer((client, _) =>
        {
            client.Client.LingerState = new LingerOption(true, 0);
            client.Close();
            return Task.CompletedTask;
        });
        var services = new ServiceCollection();
        services.AddBingResilience().AddFencesProvider("test").UseDefaultProvider("test");
        services.AddHttpClient("reset").AddBingHttpResilience("loopback", o =>
        {
            o.CanRetryRequest = _ => true;
            o.Retry.Delay = TimeSpan.Zero;
            o.Retry.MaxRetryAttempts = 1;
            o.CircuitBreaker.Enabled = false;
        });
        using var provider = services.BuildServiceProvider();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("reset");
        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(server.Url).WaitAsync(TimeSpan.FromSeconds(5)));
        // SocketsHttpHandler 自身也可能重建连接；精确管线次数由可控 handler 测试断言。
        Assert.True(server.Connections >= 2);
    }

    private sealed class LoopbackServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _accept;
        private readonly List<Task> _requests = new();
        private int _connections;
        internal int Connections => Volatile.Read(ref _connections);
        internal string Url { get; }
        internal LoopbackServer(Func<TcpClient, CancellationToken, Task> respond)
        {
            _listener.Start();
            Url = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/";
            _accept = Accept(respond);
        }
        private async Task Accept(Func<TcpClient, CancellationToken, Task> respond)
        {
            try
            {
                while (!_stop.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                    Interlocked.Increment(ref _connections);
                    _requests.Add(Serve(client, respond));
                }
            }
            catch (OperationCanceledException) { }
        }
        private async Task Serve(TcpClient client, Func<TcpClient, CancellationToken, Task> respond)
        {
            using (client)
            {
                try
                {
                    using var reader = new StreamReader(client.GetStream(), Encoding.ASCII, false, 1024, true);
                    while (!string.IsNullOrEmpty(await reader.ReadLineAsync().WaitAsync(_stop.Token))) { }
                    await respond(client, _stop.Token);
                }
                catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
                catch (IOException) { } // 取消的客户端可能先关闭连接。
            }
        }
        public async ValueTask DisposeAsync()
        {
            _stop.Cancel();
            await _accept;
            await Task.WhenAll(_requests);
            _listener.Stop();
            _stop.Dispose();
        }
    }
}

using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Bing.Samples.WebApi;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Bing.Samples.WebApi.Tests;

/// <summary>
/// 验证 WebApi 示例的属性注入、延迟解析和正常关闭链路。
/// </summary>
public sealed class SampleWebApiSmokeTest
{
    /// <summary>
    /// 验证示例请求返回业务值并在宿主停止后释放监听端口。
    /// </summary>
    [Fact]
    public async Task LazyEndpoint_ShouldResolveBusinessValueAndReleasePortOnStop()
    {
        using var host = Program.CreateHostBuilder(Array.Empty<string>())
            .ConfigureWebHostDefaults(webBuilder => webBuilder.UseUrls("http://127.0.0.1:0"))
            .Build();

        await host.StartAsync();
        var address = host.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()?
            .Addresses
            .Single(item => item.StartsWith("http://127.0.0.1:", StringComparison.Ordinal));
        Assert.False(string.IsNullOrWhiteSpace(address));

        var endpoint = new Uri(address!);
        using var client = new HttpClient { BaseAddress = endpoint };
        using var response = await client.GetAsync("/sample/lazy");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"Injected\":true", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("lazy-service-ready", body, StringComparison.Ordinal);

        await host.StopAsync();
        await AssertPortIsClosedAsync(endpoint.Port);
    }

    /// <summary>
    /// 验证宿主停止后端口不能继续建立连接。
    /// </summary>
    private static async Task AssertPortIsClosedAsync(int port)
    {
        using var client = new TcpClient();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            await client.ConnectAsync(IPAddress.Loopback, port, cancellation.Token);
            throw new Xunit.Sdk.XunitException($"端口 {port} 在宿主停止后仍可连接。");
        }
        catch (SocketException)
        {
        }
        catch (OperationCanceledException)
        {
            throw new Xunit.Sdk.XunitException($"端口 {port} 在检查期限内未确认关闭。");
        }
    }
}

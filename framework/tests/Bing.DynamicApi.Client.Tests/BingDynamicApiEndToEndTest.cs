using Bing.Application.Services;
using Bing.AspNetCore.Mvc.DynamicApi;
using Bing.DynamicApi.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Bing.DynamicApi.Client.Tests;

/// <summary>
/// 验证动态客户端通过 TestServer 调用 MVC 动态 API。
/// </summary>
public class BingDynamicApiEndToEndTest
{
    /// <summary>
    /// 验证客户端读取实时 API 描述并调用查询和原始文本端点。
    /// </summary>
    [Fact]
    public async Task DynamicClient_UsesLiveDefinitionAndCallsMvcActionThroughTestServer()
    {
        using var server = new HostBuilder()
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddScoped<IE2EDynamicApiAppService, E2EDynamicApiAppService>();
                    services.AddBingDynamicApi(typeof(E2EDynamicApiAppService).Assembly, options =>
                        options.TypePredicate = type => type == typeof(E2EDynamicApiAppService));
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                }))
            .Build();
        await server.StartAsync();

        var services = new ServiceCollection();
        services.AddBingDynamicApiClient(options => options.AddRemoteService("e2e", remote =>
        {
            remote.BaseUrl = "http://localhost";
            remote.ApiVersion = "v1";
        }));
        services.AddHttpClient("e2e")
            .ConfigurePrimaryHttpMessageHandler(() => server.GetTestServer().CreateHandler());
        services.AddBingDynamicApiClient<IE2EDynamicApiAppService>("e2e");
        await using var clientProvider = services.BuildServiceProvider();
        var client = clientProvider.GetRequiredService<IE2EDynamicApiAppService>();
        var id = Guid.Parse("9b381574-f6bc-4131-9e74-087e5a2d6d21");

        var result = await client.LookupAsync(id, "search & value", new E2EDynamicApiInput
        {
            Name = "payload",
            Count = 6,
        }, CancellationToken.None);

        Assert.Equal($"{id:D}:search & value:payload", result.Name);
        Assert.Equal(6, result.Count);

        var rawText = await client.GetRawTextAsync(CancellationToken.None);

        Assert.Equal("plain:text-response", rawText);
    }

    /// <summary>
    /// 验证客户端支持泛型、重载方法及不同参数类型。
    /// </summary>
    [Fact]
    public async Task DynamicClient_MatchesGenericAndOverloadedParameterTypesThroughTestServer()
    {
        using var server = new HostBuilder()
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddScoped<IE2EDynamicApiAppService, E2EDynamicApiAppService>();
                    services.AddBingDynamicApi(typeof(E2EDynamicApiAppService).Assembly, options =>
                        options.TypePredicate = type => type == typeof(E2EDynamicApiAppService));
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                }))
            .Build();
        await server.StartAsync();

        var services = new ServiceCollection();
        services.AddBingDynamicApiClient(options => options.AddRemoteService("e2e", remote =>
        {
            remote.BaseUrl = "http://localhost";
            remote.ApiVersion = "v1";
        }));
        services.AddHttpClient("e2e")
            .ConfigurePrimaryHttpMessageHandler(() => server.GetTestServer().CreateHandler());
        services.AddBingDynamicApiClient<IE2EDynamicApiAppService>("e2e");
        await using var clientProvider = services.BuildServiceProvider();
        var client = clientProvider.GetRequiredService<IE2EDynamicApiAppService>();

        var integerResult = await client.ProcessAsync(new List<int> { 2, 4 });
        var stringResult = await client.ProcessAsync(new List<string> { "alpha", "beta" });
        var genericResult = await client.ProcessEnvelopeAsync(new E2EDynamicApiEnvelope<E2EDynamicApiInput>
        {
            Data = new E2EDynamicApiInput { Name = "generic", Count = 9 },
        });

        Assert.Equal("integers:2,4", integerResult.Name);
        Assert.Equal("strings:alpha,beta", stringResult.Name);
        Assert.Equal("envelope:generic:9", genericResult.Name);
    }

    /// <summary>
    /// 定义端到端测试使用的动态应用服务契约。
    /// </summary>
    public interface IE2EDynamicApiAppService : IAppService
    {
        /// <summary>
        /// 按标识、查询词和请求体查询数据。
        /// </summary>
        /// <param name="id">查询对象标识。</param>
        /// <param name="term">查询词。</param>
        /// <param name="input">查询请求体。</param>
        /// <param name="cancellationToken">用于取消请求的令牌。</param>
        /// <returns>包含查询结果的异步操作。</returns>
        [HttpPost("lookup/{id}")]
        Task<E2EDynamicApiResult> LookupAsync(
            Guid id,
            [FromQuery(Name = "term")] string term,
            E2EDynamicApiInput input,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取原始文本响应。
        /// </summary>
        /// <param name="cancellationToken">用于取消请求的令牌。</param>
        /// <returns>包含原始文本的异步操作。</returns>
        [HttpGet("raw-text")]
        Task<string> GetRawTextAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 处理整数列表。
        /// </summary>
        /// <param name="values">待处理的整数列表。</param>
        /// <returns>包含处理结果的异步操作。</returns>
        [HttpPost("process/integers")]
        Task<E2EDynamicApiResult> ProcessAsync(List<int> values);

        /// <summary>
        /// 处理字符串列表。
        /// </summary>
        /// <param name="values">待处理的字符串列表。</param>
        /// <returns>包含处理结果的异步操作。</returns>
        [HttpPost("process/strings")]
        Task<E2EDynamicApiResult> ProcessAsync(List<string> values);

        /// <summary>
        /// 处理泛型数据封装对象。
        /// </summary>
        /// <param name="input">包含输入数据的封装对象。</param>
        /// <returns>包含处理结果的异步操作。</returns>
        [HttpPost("process/envelope")]
        Task<E2EDynamicApiResult> ProcessEnvelopeAsync(E2EDynamicApiEnvelope<E2EDynamicApiInput> input);
    }

    /// <summary>
    /// 提供端到端测试使用的动态应用服务实现。
    /// </summary>
    public sealed class E2EDynamicApiAppService : IE2EDynamicApiAppService
    {
        /// <inheritdoc />
        public Task<E2EDynamicApiResult> LookupAsync(
            Guid id,
            string term,
            E2EDynamicApiInput input,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new E2EDynamicApiResult
            {
                Name = $"{id:D}:{term}:{input.Name}",
                Count = input.Count,
            });
        }

        /// <inheritdoc />
        public Task<string> GetRawTextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult("plain:text-response");
        }

        /// <inheritdoc />
        public Task<E2EDynamicApiResult> ProcessAsync(List<int> values) =>
            Task.FromResult(new E2EDynamicApiResult { Name = $"integers:{string.Join(",", values)}" });

        /// <inheritdoc />
        public Task<E2EDynamicApiResult> ProcessAsync(List<string> values) =>
            Task.FromResult(new E2EDynamicApiResult { Name = $"strings:{string.Join(",", values)}" });

        /// <inheritdoc />
        public Task<E2EDynamicApiResult> ProcessEnvelopeAsync(E2EDynamicApiEnvelope<E2EDynamicApiInput> input) =>
            Task.FromResult(new E2EDynamicApiResult { Name = $"envelope:{input.Data.Name}:{input.Data.Count}" });
    }

    /// <summary>
    /// 表示端到端查询和泛型测试的输入数据。
    /// </summary>
    public sealed class E2EDynamicApiInput
    {
        /// <summary>
        /// 获取或设置输入名称。
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置输入数量。
        /// </summary>
        public int Count { get; set; }
    }

    /// <summary>
    /// 表示端到端测试使用的泛型数据封装。
    /// </summary>
    /// <typeparam name="T">封装的数据类型。</typeparam>
    public sealed class E2EDynamicApiEnvelope<T>
    {
        /// <summary>
        /// 获取或设置封装的数据。
        /// </summary>
        public T Data { get; set; }
    }

    /// <summary>
    /// 表示端到端动态 API 的响应数据。
    /// </summary>
    public sealed class E2EDynamicApiResult
    {
        /// <summary>
        /// 获取或设置响应名称。
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置响应数量。
        /// </summary>
        public int Count { get; set; }
    }
}

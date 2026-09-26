#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Bing.Application.Services;
using Bing.AspNetCore.Mvc;
using Bing.AspNetCore.Mvc.DynamicApi;
using Bing.Content;
using Bing.DynamicApi.WebApiClient;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebApiClientCore;
using WebApiClientCore.Attributes;
using WebApiClientCore.Exceptions;
using WebApiClientCore.Parameters;
using ClientHttpGet = WebApiClientCore.Attributes.HttpGetAttribute;
using ClientHttpPost = WebApiClientCore.Attributes.HttpPostAttribute;
using MvcHttpGet = Microsoft.AspNetCore.Mvc.HttpGetAttribute;
using MvcHttpPost = Microsoft.AspNetCore.Mvc.HttpPostAttribute;
using Xunit;

namespace Bing.DynamicApi.WebApiClient.Tests;

/// <summary>
/// 验证 WebApiClient SDK 与动态 MVC API 的端到端集成。
/// </summary>
public sealed class BingWebApiClientEndToEndTest : IClassFixture<WebApiClientTestServerFixture>
{
    /// <summary>
    /// 获取端到端测试服务器 fixture。
    /// </summary>
    private readonly WebApiClientTestServerFixture _fixture;

    /// <summary>
    /// 初始化 BingWebApiClientEndToEndTest 类的新实例。
    /// </summary>
    /// <param name="fixture">提供测试服务器和请求探针的 fixture。</param>
    public BingWebApiClientEndToEndTest(WebApiClientTestServerFixture fixture) => _fixture = fixture;

    /// <summary>
    /// 验证声明式路由与动态 MVC 端点及 JSON 请求响应一致。
    /// </summary>
    [Fact]
    public async Task DeclaredRoutesAndJsonPayloadsMatchDynamicMvcEndpoints()
    {
        using var client = CreateClient();
        var api = client.GetRequiredService<IOrdersSdk>();
        var id = Guid.Parse("a4d36ce8-6119-4f86-ae1d-f65e7218a198");

        var found = await api.GetByIdAsync(id, "alpha & beta", CancellationToken.None);
        Assert.Equal(id, found.Id);
        Assert.Equal("alpha & beta", found.Name);
        Assert.Equal("GET", _fixture.Probe.LastRequest.Method);
        Assert.Equal($"/api/app/v1/orders/{id:D}", _fixture.Probe.LastRequest.Path);
        Assert.Equal("?term=alpha%20%26%20beta", _fixture.Probe.LastRequest.Query);

        var created = await api.CreateAsync(new OrderInput { Name = "created", Quantity = 4 }, CancellationToken.None);
        Assert.Equal("created", created.Name);
        Assert.Equal(4, created.Quantity);
        Assert.Equal("POST", _fixture.Probe.LastRequest.Method);
        Assert.Equal("/api/app/v1/orders", _fixture.Probe.LastRequest.Path);
    }

    /// <summary>
    /// 验证 multipart 上传、原始文本和下载响应的资源所有权约定。
    /// </summary>
    [Fact]
    public async Task MultipartUploadRawTextAndDownloadKeepTheirExpectedOwnership()
    {
        using var client = CreateClient();
        var api = client.GetRequiredService<IOrdersSdk>();

        var uploadAction = _fixture.Server.Services.GetRequiredService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Single(action => action.MethodInfo.Name == nameof(IOrdersAppService.UploadAsync));
        Assert.Equal("FormFile", uploadAction.Parameters.Single(parameter => parameter.Name == "file").BindingInfo?.BindingSource?.Id);

        var rawText = await api.GetRawTextAsync(CancellationToken.None);
        Assert.Equal("plain:raw-response", rawText);

        var uploaded = await api.UploadAsync(
            new OrderUploadInput { Description = "SDK upload" },
            new FormDataFile(Encoding.UTF8.GetBytes("multipart-body"), "payload.txt"),
            CancellationToken.None);
        Assert.Equal("SDK upload|multipart-body", uploaded);
        Assert.Equal("POST", _fixture.Probe.LastRequest.Method);
        Assert.Equal("/api/app/v1/orders/upload", _fixture.Probe.LastRequest.Path);
        Assert.StartsWith("multipart/form-data; boundary=", _fixture.Probe.LastRequest.ContentType, StringComparison.OrdinalIgnoreCase);

        using var response = await api.DownloadAsync(CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("download.txt", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.Equal("download-body", await response.Content.ReadAsStringAsync());
        Assert.Equal("GET", _fixture.Probe.LastRequest.Method);
        Assert.Equal("/api/app/v1/orders/download", _fixture.Probe.LastRequest.Path);
    }

    /// <summary>
    /// 验证上下文请求头和宿主认证处理器会应用到每次请求。
    /// </summary>
    [Fact]
    public async Task ContextHeadersAndHostAuthenticationHandlerAreAppliedPerRequest()
    {
        var contextAccessor = new TestRequestContextAccessor();
        using var client = CreateClient(contextAccessor, builder =>
        {
            builder.ConfigureHttpApi(options => options.HttpHost = new Uri("http://app-configured.example/"));
            builder.ConfigureHttpClient(httpClient =>
            {
                httpClient.BaseAddress = new Uri("http://client-base.example/");
                httpClient.DefaultRequestHeaders.Add("X-Sdk-Default", "host-default");
            });
        });
        var api = client.GetRequiredService<IOrdersSdk>();

        contextAccessor.Current = new BingWebApiClientRequestContext("tenant-17", "corr-29", "fr-CA");
        var headers = await api.GetHeadersAsync(CancellationToken.None);
        Assert.Equal("tenant-17", headers.TenantId);
        Assert.Equal("corr-29", headers.CorrelationId);
        Assert.Equal("fr-CA", headers.Language);
        Assert.Equal("host-default", headers.SdkDefault);
        Assert.Equal("app-configured.example", client.GetRequiredService<TestCredentialState>().LastRequestUri?.Host);
        var clientState = client.GetRequiredService<TestCredentialState>();
        var configuredClient = client.GetRequiredService<IHttpClientFactory>().CreateClient(clientState.HttpClientName);
        Assert.Equal(new Uri("http://client-base.example/"), configuredClient.BaseAddress);

        var unauthorized = await Assert.ThrowsAsync<HttpRequestException>(() => api.GetProtectedAsync(CancellationToken.None));
        var unauthorizedStatus = Assert.IsType<ApiResponseStatusException>(unauthorized.InnerException);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedStatus.StatusCode);
        unauthorizedStatus.ResponseMessage.Dispose();

        client.GetRequiredService<TestCredentialState>().Token = "sdk-secret";
        Assert.Equal("authorized", await api.GetProtectedAsync(CancellationToken.None));
    }

    /// <summary>
    /// 验证请求上下文访问器会在每次请求时重新读取。
    /// </summary>
    [Fact]
    public async Task ContextAccessorIsReadForEveryRequest()
    {
        var contextAccessor = new TestRequestContextAccessor();
        using var client = CreateClient(contextAccessor);
        var api = client.GetRequiredService<IOrdersSdk>();

        contextAccessor.Current = new BingWebApiClientRequestContext("tenant-first", "corr-first", "en-US");
        var first = await api.GetHeadersAsync(CancellationToken.None);
        contextAccessor.Current = new BingWebApiClientRequestContext("tenant-second", "corr-second", "zh-CN");
        var second = await api.GetHeadersAsync(CancellationToken.None);

        Assert.Equal("tenant-first", first.TenantId);
        Assert.Equal("corr-first", first.CorrelationId);
        Assert.Equal("en-US", first.Language);
        Assert.Equal("tenant-second", second.TenantId);
        Assert.Equal("corr-second", second.CorrelationId);
        Assert.Equal("zh-CN", second.Language);
    }

    /// <summary>
    /// 验证缺失或不完整的上下文会清理默认请求头中的旧值。
    /// </summary>
    [Fact]
    public async Task ContextHeadersClearDefaultValuesWhenContextIsMissingOrIncomplete()
    {
        var contextAccessor = new TestRequestContextAccessor
        {
            Current = new BingWebApiClientRequestContext("tenant-first", "corr-first", "en-US"),
        };
        using var client = CreateClient(contextAccessor, configureBuilder: builder =>
            builder.ConfigureHttpClient(httpClient =>
            {
                httpClient.DefaultRequestHeaders.Add("X-Tenant-Id", "stale-tenant");
                httpClient.DefaultRequestHeaders.Add("X-Correlation-Id", "stale-correlation");
                httpClient.DefaultRequestHeaders.Add("Accept-Language", "stale-language");
            }));
        var api = client.GetRequiredService<IOrdersSdk>();

        var populated = await api.GetHeadersAsync(CancellationToken.None);
        Assert.Equal("tenant-first", populated.TenantId);
        Assert.Equal("corr-first", populated.CorrelationId);
        Assert.Equal("en-US", populated.Language);

        contextAccessor.Current = new BingWebApiClientRequestContext("tenant-second", null, string.Empty);
        var incomplete = await api.GetHeadersAsync(CancellationToken.None);
        Assert.Equal("tenant-second", incomplete.TenantId);
        Assert.Equal(string.Empty, incomplete.CorrelationId);
        Assert.Equal(string.Empty, incomplete.Language);

        contextAccessor.Current = null;
        var empty = await api.GetHeadersAsync(CancellationToken.None);
        Assert.Equal(string.Empty, empty.TenantId);
        Assert.Equal(string.Empty, empty.CorrelationId);
        Assert.Equal(string.Empty, empty.Language);
    }

    /// <summary>
    /// 验证并发请求之间的上下文请求头相互隔离。
    /// </summary>
    [Fact]
    public async Task ContextHeadersRemainIsolatedForConcurrentRequests()
    {
        const int requestCount = 8;
        var contextAccessor = new TestRequestContextAccessor();
        using var client = CreateClient(contextAccessor);
        var api = client.GetRequiredService<IOrdersSdk>();
        var startRequests = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var readyRequests = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var readyCount = 0;
        _fixture.Probe.ExpectConcurrentRequests(requestCount);

        var requests = Enumerable.Range(1, requestCount).Select(index => Task.Run(async () =>
        {
            contextAccessor.Current = new BingWebApiClientRequestContext(
                $"tenant-{index}",
                $"corr-{index}",
                $"lang-{index}");

            if (Interlocked.Increment(ref readyCount) == requestCount)
                readyRequests.TrySetResult(true);

            await startRequests.Task.WaitAsync(TimeSpan.FromSeconds(5));
            return await api.GetHeadersAsync(CancellationToken.None);
        })).ToArray();

        try
        {
            await readyRequests.Task.WaitAsync(TimeSpan.FromSeconds(5));
            startRequests.TrySetResult(true);

            var responses = await Task.WhenAll(requests);
            for (var index = 1; index <= requestCount; index++)
            {
                var headers = responses[index - 1];
                Assert.Equal($"tenant-{index}", headers.TenantId);
                Assert.Equal($"corr-{index}", headers.CorrelationId);
                Assert.Equal($"lang-{index}", headers.Language);
            }
        }
        finally
        {
            startRequests.TrySetResult(true);
            _fixture.Probe.ResetConcurrentRequests();
        }
    }

    /// <summary>
    /// 验证业务失败状态保留 WebApiClient 的异常形态。
    /// </summary>
    [Fact]
    public async Task NonSuccessBusinessStatusUsesWebApiClientExceptionShape()
    {
        using var client = CreateClient();
        var api = client.GetRequiredService<IOrdersSdk>();

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => api.GetConflictAsync(CancellationToken.None));
        var statusException = Assert.IsType<ApiResponseStatusException>(exception.InnerException);
        Assert.Equal(HttpStatusCode.Conflict, statusException.StatusCode);
        statusException.ResponseMessage.Dispose();
    }

    /// <summary>
    /// 验证上下文请求头名称可以自定义或禁用。
    /// </summary>
    [Fact]
    public async Task ContextHeaderNamesCanBeCustomizedOrDisabled()
    {
        var contextAccessor = new TestRequestContextAccessor
        {
            Current = new BingWebApiClientRequestContext("tenant-custom", "correlation-custom", "ja-JP"),
        };
        using var client = CreateClient(
            contextAccessor,
            configureBuilder: builder => builder.ConfigureHttpClient(httpClient =>
                httpClient.DefaultRequestHeaders.Add("X-Correlation-Id", "host-correlation")),
            configureOptions: options =>
            {
                options.TenantHeaderName = "X-Context-Tenant";
                options.CorrelationIdHeaderName = null;
                options.LanguageHeaderName = "X-Context-Language";
            });

        var headers = await client.GetRequiredService<IOrdersSdk>().GetHeadersAsync(CancellationToken.None);

        Assert.Equal(string.Empty, headers.TenantId);
        Assert.Equal("host-correlation", headers.CorrelationId);
        Assert.Equal(string.Empty, headers.Language);
        Assert.Equal("tenant-custom", headers.CustomTenantId);
        Assert.Equal(string.Empty, headers.CustomCorrelationId);
        Assert.Equal("ja-JP", headers.CustomLanguage);
    }

    /// <summary>
    /// 验证取消令牌可以取消正在执行的动态 API 请求。
    /// </summary>
    [Fact]
    public async Task CancellationTokenCancelsTheInFlightDynamicApiRequest()
    {
        using var client = CreateClient();
        var api = client.GetRequiredService<IOrdersSdk>();
        using var cancellation = new CancellationTokenSource();

        var request = api.WaitAsync(cancellation.Token);
        await _fixture.Probe.WaitStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => request);
        Assert.IsType<TaskCanceledException>(exception.InnerException);
        await _fixture.Probe.WaitCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// 验证注册会拒绝无效基础地址、非接口契约和非法请求头名称。
    /// </summary>
    [Fact]
    public void RegistrationRejectsInvalidBaseAddressesAndNonInterfaceContracts()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() => services.AddBingWebApiClient<IOrdersSdk>(options =>
            options.BaseAddress = new Uri("relative/path", UriKind.Relative)));
        Assert.Throws<InvalidOperationException>(() => services.AddBingWebApiClient<IOrdersSdk>(options =>
            options.BaseAddress = new Uri("ftp://orders.example/")));
        Assert.Throws<InvalidOperationException>(() => services.AddBingWebApiClient<IOrdersSdk>(options =>
            options.BaseAddress = new Uri("https://orders.example/?tenant=one")));
        Assert.Throws<InvalidOperationException>(() => services.AddBingWebApiClient<IOrdersSdk>(options =>
            options.BaseAddress = new Uri("https://orders.example/#fragment")));
        Assert.Throws<ArgumentException>(() => services.AddBingWebApiClient<ConcreteSdkContract>(options =>
            options.BaseAddress = new Uri("https://localhost/")));

        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddBingWebApiClient<IOrdersSdk>(options =>
        {
            options.BaseAddress = new Uri("https://orders.example/");
            options.TenantHeaderName = string.Empty;
        }));
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddBingWebApiClient<IOrdersSdk>(options =>
        {
            options.BaseAddress = new Uri("https://orders.example/");
            options.CorrelationIdHeaderName = "Invalid Header";
        }));
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddBingWebApiClient<IOrdersSdk>(options =>
        {
            options.BaseAddress = new Uri("https://orders.example/");
            options.LanguageHeaderName = "Invalid\r\nHeader";
        }));

        var validServices = new ServiceCollection();
        validServices.AddBingWebApiClient<IOrdersSdk>(options => options.BaseAddress = new Uri("https://orders.example/"));
        Assert.Equal(1, validServices.Count(descriptor => descriptor.ServiceType == typeof(IOrdersSdk)));
    }

    /// <summary>
    /// 验证宿主未提供上下文访问器时使用空访问器。
    /// </summary>
    [Fact]
    public async Task RegistrationUsesEmptyContextAccessorWhenHostDoesNotProvideOne()
    {
        var services = new ServiceCollection();
        var credentialState = new TestCredentialState();
        services.AddSingleton(credentialState);
        var builder = services.AddBingWebApiClient<IOrdersSdk>(options =>
            options.BaseAddress = new Uri("http://localhost"));
        credentialState.HttpClientName = builder.Name;
        builder.ConfigurePrimaryHttpMessageHandler(() => _fixture.Server.GetTestServer().CreateHandler());

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var accessor = provider.GetRequiredService<IBingWebApiClientRequestContextAccessor>();

        Assert.Null(accessor.GetCurrentContext());
        Assert.Equal("plain:raw-response", await provider.GetRequiredService<IOrdersSdk>()
            .GetRawTextAsync(CancellationToken.None));
    }

    /// <summary>
    /// 验证扩展注册会保留预先配置的 HttpHost。
    /// </summary>
    [Fact]
    public async Task RegistrationPreservesHttpHostConfiguredBeforeTheExtension()
    {
        var services = new ServiceCollection();
        var credentialState = new TestCredentialState();
        services.AddSingleton(credentialState);
        services.AddTransient<TestAuthorizationMessageHandler>();
        services.ConfigureHttpApi<IOrdersSdk>(options => options.HttpHost = new Uri("http://preconfigured.example/"));

        var builder = services.AddBingWebApiClient<IOrdersSdk>(options =>
            options.BaseAddress = new Uri("http://fallback.example/"));
        credentialState.HttpClientName = builder.Name;
        builder.ConfigurePrimaryHttpMessageHandler(() => _fixture.Server.GetTestServer().CreateHandler());
        builder.AddHttpMessageHandler<TestAuthorizationMessageHandler>();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        await provider.GetRequiredService<IOrdersSdk>().GetRawTextAsync(CancellationToken.None);

        Assert.Equal("preconfigured.example", credentialState.LastRequestUri?.Host);
    }

    /// <summary>
    /// 验证多个 SDK 契约的地址、请求头和消息处理器彼此隔离。
    /// </summary>
    [Fact]
    public async Task MultipleSdkContractsKeepTheirHeaderConfigurationIndependent()
    {
        var contextAccessor = new TestRequestContextAccessor
        {
            Current = new BingWebApiClientRequestContext("tenant-shared", "corr-shared", "zh-CN"),
        };
        var services = new ServiceCollection();
        services.AddSingleton<IBingWebApiClientRequestContextAccessor>(contextAccessor);
        services.AddTransient<OrdersSdkMarkerHandler>();
        services.AddTransient<AlternateOrdersSdkMarkerHandler>();

        var firstBuilder = services.AddBingWebApiClient<IOrdersSdk>(options =>
            options.BaseAddress = new Uri("http://orders-a.example/"));
        firstBuilder.ConfigurePrimaryHttpMessageHandler(() => _fixture.Server.GetTestServer().CreateHandler());
        firstBuilder.ConfigureHttpClient(httpClient => httpClient.DefaultRequestHeaders.Add("X-Sdk-A", "sdk-a"));
        firstBuilder.AddHttpMessageHandler<OrdersSdkMarkerHandler>();

        var secondBuilder = services.AddBingWebApiClient<IAlternateOrdersSdk>(options =>
        {
            options.BaseAddress = new Uri("http://orders-b.example/");
            options.TenantHeaderName = "X-Alternate-Tenant";
            options.CorrelationIdHeaderName = "X-Alternate-Correlation";
            options.LanguageHeaderName = "X-Alternate-Language";
        });
        secondBuilder.ConfigurePrimaryHttpMessageHandler(() => _fixture.Server.GetTestServer().CreateHandler());
        secondBuilder.ConfigureHttpClient(httpClient => httpClient.DefaultRequestHeaders.Add("X-Sdk-B", "sdk-b"));
        secondBuilder.AddHttpMessageHandler<AlternateOrdersSdkMarkerHandler>();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var first = await provider.GetRequiredService<IOrdersSdk>().GetHeadersAsync(CancellationToken.None);
        var second = await provider.GetRequiredService<IAlternateOrdersSdk>().GetHeadersAsync(CancellationToken.None);

        Assert.Equal("tenant-shared", first.TenantId);
        Assert.Equal("corr-shared", first.CorrelationId);
        Assert.Equal("zh-CN", first.Language);
        Assert.Equal("orders-a.example", first.RequestHost);
        Assert.Equal("sdk-a", first.SdkA);
        Assert.Equal(string.Empty, first.SdkB);
        Assert.Equal("handler-a", first.SdkHandlerA);
        Assert.Equal(string.Empty, first.SdkHandlerB);
        Assert.Equal(string.Empty, first.CustomTenantId);
        Assert.Equal(string.Empty, first.CustomCorrelationId);
        Assert.Equal(string.Empty, first.CustomLanguage);

        Assert.Equal(string.Empty, second.TenantId);
        Assert.Equal(string.Empty, second.CorrelationId);
        Assert.Equal(string.Empty, second.Language);
        Assert.Equal("tenant-shared", second.AlternateTenantId);
        Assert.Equal("corr-shared", second.AlternateCorrelationId);
        Assert.Equal("zh-CN", second.AlternateLanguage);
        Assert.Equal("orders-b.example", second.RequestHost);
        Assert.Equal(string.Empty, second.SdkA);
        Assert.Equal("sdk-b", second.SdkB);
        Assert.Equal(string.Empty, second.SdkHandlerA);
        Assert.Equal("handler-b", second.SdkHandlerB);
    }

    /// <summary>
    /// 创建订单 SDK 测试客户端及其可选配置。
    /// </summary>
    /// <param name="contextAccessor">可选的请求上下文访问器。</param>
    /// <param name="configureBuilder">可选的 HTTP 客户端构建器配置。</param>
    /// <param name="configureOptions">可选的 WebApiClient SDK 选项配置。</param>
    /// <returns>包含订单 SDK 和测试消息处理器的服务提供程序。</returns>
    private ServiceProvider CreateClient(
        TestRequestContextAccessor? contextAccessor = null,
        Action<IHttpClientBuilder>? configureBuilder = null,
        Action<BingWebApiClientOptions>? configureOptions = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IBingWebApiClientRequestContextAccessor>(contextAccessor ?? new TestRequestContextAccessor());
        var credentialState = new TestCredentialState();
        services.AddSingleton(credentialState);
        services.AddTransient<TestAuthorizationMessageHandler>();

        var builder = services.AddBingWebApiClient<IOrdersSdk>(options =>
        {
            options.BaseAddress = new Uri("http://localhost");
            configureOptions?.Invoke(options);
        });
        credentialState.HttpClientName = builder.Name;
        builder.ConfigurePrimaryHttpMessageHandler(() => _fixture.Server.GetTestServer().CreateHandler());
        builder.AddHttpMessageHandler<TestAuthorizationMessageHandler>();
        configureBuilder?.Invoke(builder);

        return services.BuildServiceProvider(validateScopes: true);
    }
}

/// <summary>
/// 定义第三方订单 SDK 的声明式 HTTP 契约。
/// </summary>
public interface IOrdersSdk
{
    /// <summary>
    /// 按标识和查询词获取订单。
    /// </summary>
    /// <param name="id">订单标识。</param>
    /// <param name="term">订单查询词。</param>
    /// <param name="cancellationToken">用于取消请求的令牌。</param>
    /// <returns>包含订单数据的异步操作。</returns>
    [ClientHttpGet("api/app/v1/orders/{id}")]
    [JsonReturn]
    Task<OrderDto> GetByIdAsync([PathQuery] Guid id, [PathQuery] string term, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步创建订单。
    /// </summary>
    /// <param name="input">订单创建数据。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>已创建的订单。</returns>
    [ClientHttpPost("api/app/v1/orders")]
    [JsonReturn]
    Task<OrderDto> CreateAsync([JsonContent] OrderInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步获取原始文本响应。
    /// </summary>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>服务端的原始文本。</returns>
    [ClientHttpGet("api/app/v1/orders/raw-text")]
    [RawReturn]
    Task<string> GetRawTextAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步上传订单文件及附加数据。
    /// </summary>
    /// <param name="input">文件附加数据。</param>
    /// <param name="file">待上传的文件。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>文件描述与上传文本组成的结果。</returns>
    [ClientHttpPost("api/app/v1/orders/upload")]
    [RawReturn]
    Task<string> UploadAsync([FormDataContent] OrderUploadInput input, FormDataFile file, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步下载订单文件。
    /// </summary>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>包含下载内容的响应。</returns>
    /// <remarks>调用方负责释放响应及其内容。</remarks>
    [ClientHttpGet("api/app/v1/orders/download")]
    [RawReturn]
    Task<HttpResponseMessage> DownloadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步获取服务端收到的请求头。
    /// </summary>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>服务端回显的请求头值。</returns>
    [ClientHttpGet("api/app/v1/orders/headers")]
    [JsonReturn]
    Task<OrderHeadersDto> GetHeadersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步调用受认证保护的订单端点。
    /// </summary>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>授权成功的响应文本。</returns>
    [ClientHttpGet("api/app/v1/orders/protected")]
    [RawReturn]
    Task<string> GetProtectedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步调用业务冲突测试端点。
    /// </summary>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>契约声明的订单结果；测试端点实际返回冲突错误。</returns>
    [ClientHttpGet("api/app/v1/orders/conflict")]
    [JsonReturn]
    Task<OrderDto> GetConflictAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步调用等待取消信号的测试端点。
    /// </summary>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>契约声明的文本结果；正常测试通过取消结束调用。</returns>
    [ClientHttpGet("api/app/v1/orders/wait")]
    [RawReturn]
    Task<string> WaitAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 定义使用独立上下文请求头配置的备用订单 SDK 契约。
/// </summary>
public interface IAlternateOrdersSdk
{
    /// <summary>
    /// 获取服务端收到的请求头信息。
    /// </summary>
    /// <param name="cancellationToken">用于取消请求的令牌。</param>
    /// <returns>包含请求头值的异步操作。</returns>
    [ClientHttpGet("api/app/v1/orders/headers")]
    [JsonReturn]
    Task<OrderHeadersDto> GetHeadersAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 定义动态 MVC 服务端的订单应用服务契约。
/// </summary>
public interface IOrdersAppService : IAppService
{
    /// <summary>
    /// 按标识和查询词获取订单。
    /// </summary>
    /// <param name="id">订单标识。</param>
    /// <param name="term">订单查询词。</param>
    /// <param name="cancellationToken">用于取消查询的令牌。</param>
    /// <returns>包含订单数据的异步操作。</returns>
    [MvcHttpGet("{id}")]
    Task<OrderDto> GetByIdAsync(Guid id, [FromQuery(Name = "term")] string term, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步创建订单。
    /// </summary>
    /// <param name="input">订单创建数据。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>已创建的订单。</returns>
    [MvcHttpPost]
    Task<OrderDto> CreateAsync(OrderInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步获取原始文本响应。
    /// </summary>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>服务端的原始文本。</returns>
    [MvcHttpGet("raw-text")]
    Task<string> GetRawTextAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步读取上传的订单文件。
    /// </summary>
    /// <param name="file">上传的远程文件内容。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>文件描述与上传文本组成的结果。</returns>
    [MvcHttpPost("upload")]
    Task<string> UploadAsync(IRemoteStreamContent file, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步获取供下载测试使用的文件。
    /// </summary>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>远程文件内容。</returns>
    [MvcHttpGet("download")]
    Task<IRemoteStreamContent> DownloadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步获取服务端收到的请求头。
    /// </summary>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>服务端回显的请求头值。</returns>
    [MvcHttpGet("headers")]
    Task<OrderHeadersDto> GetHeadersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步获取受认证保护的订单响应。
    /// </summary>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>授权成功的响应文本。</returns>
    [Authorize]
    [MvcHttpGet("protected")]
    Task<string> GetProtectedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步返回业务冲突响应。
    /// </summary>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>包含冲突状态及订单信息的操作结果。</returns>
    [MvcHttpGet("conflict")]
    Task<IActionResult> GetConflictAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步等待请求取消。
    /// </summary>
    /// <param name="cancellationToken">用于结束等待的取消令牌。</param>
    /// <returns>契约声明的文本结果；正常测试通过取消结束调用。</returns>
    [MvcHttpGet("wait")]
    Task<string> WaitAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 提供订单 SDK 集成测试使用的服务端应用服务。
/// </summary>
public sealed class OrdersAppService : IOrdersAppService
{
    /// <summary>
    /// 获取当前服务端 HTTP 上下文访问器。
    /// </summary>
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// 获取用于同步测试请求状态的探针。
    /// </summary>
    private readonly WebApiClientTestProbe _probe;

    /// <summary>
    /// 初始化 OrdersAppService 类的新实例。
    /// </summary>
    /// <param name="httpContextAccessor">提供当前请求上下文的访问器。</param>
    /// <param name="probe">用于记录请求和协调并发测试的探针。</param>
    public OrdersAppService(IHttpContextAccessor httpContextAccessor, WebApiClientTestProbe probe)
    {
        _httpContextAccessor = httpContextAccessor;
        _probe = probe;
    }

    /// <inheritdoc />
    public Task<OrderDto> GetByIdAsync(Guid id, string term, CancellationToken cancellationToken = default)
    {
        CaptureRequest();
        return Task.FromResult(new OrderDto { Id = id, Name = term });
    }

    /// <inheritdoc />
    public Task<OrderDto> CreateAsync(OrderInput input, CancellationToken cancellationToken = default)
    {
        CaptureRequest();
        return Task.FromResult(new OrderDto { Name = input.Name, Quantity = input.Quantity });
    }

    /// <inheritdoc />
    public Task<string> GetRawTextAsync(CancellationToken cancellationToken = default)
    {
        CaptureRequest();
        return Task.FromResult("plain:raw-response");
    }

    /// <inheritdoc />
    public async Task<string> UploadAsync(IRemoteStreamContent file, CancellationToken cancellationToken = default)
    {
        CaptureRequest();
        var description = (await _httpContextAccessor.HttpContext!.Request.ReadFormAsync(cancellationToken))
            ["Description"].ToString();
        using var reader = new StreamReader(file.GetStream());
        return $"{description}|{await reader.ReadToEndAsync().ConfigureAwait(false)}";
    }

    /// <inheritdoc />
    public Task<IRemoteStreamContent> DownloadAsync(CancellationToken cancellationToken = default)
    {
        CaptureRequest();
        return Task.FromResult<IRemoteStreamContent>(new RemoteStreamContent(
            new MemoryStream(Encoding.UTF8.GetBytes("download-body")), "download.txt", "text/plain"));
    }

    /// <inheritdoc />
    public async Task<OrderHeadersDto> GetHeadersAsync(CancellationToken cancellationToken = default)
    {
        CaptureRequest();
        var context = _httpContextAccessor.HttpContext!;
        var headers = context.Request.Headers;
        var result = new OrderHeadersDto
        {
            TenantId = headers["X-Tenant-Id"].ToString(),
            CorrelationId = headers["X-Correlation-Id"].ToString(),
            Language = headers["Accept-Language"].ToString(),
            SdkDefault = headers["X-Sdk-Default"].ToString(),
            CustomTenantId = headers["X-Context-Tenant"].ToString(),
            CustomCorrelationId = headers["X-Context-Correlation"].ToString(),
            CustomLanguage = headers["X-Context-Language"].ToString(),
            RequestHost = context.Request.Host.Host,
            SdkA = headers["X-Sdk-A"].ToString(),
            SdkB = headers["X-Sdk-B"].ToString(),
            SdkHandlerA = headers["X-Sdk-Handler-A"].ToString(),
            SdkHandlerB = headers["X-Sdk-Handler-B"].ToString(),
            AlternateTenantId = headers["X-Alternate-Tenant"].ToString(),
            AlternateCorrelationId = headers["X-Alternate-Correlation"].ToString(),
            AlternateLanguage = headers["X-Alternate-Language"].ToString(),
        };

        await _probe.WaitForConcurrentRequestsAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc />
    public Task<string> GetProtectedAsync(CancellationToken cancellationToken = default)
    {
        CaptureRequest();
        return Task.FromResult("authorized");
    }

    /// <inheritdoc />
    public Task<IActionResult> GetConflictAsync(CancellationToken cancellationToken = default)
    {
        CaptureRequest();
        return Task.FromResult<IActionResult>(new ConflictObjectResult(new OrderDto { Name = "business-conflict" }));
    }

    /// <inheritdoc />
    public async Task<string> WaitAsync(CancellationToken cancellationToken = default)
    {
        _probe.WaitStarted.TrySetResult(true);
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            return "unexpected";
        }
        catch (OperationCanceledException)
        {
            _probe.WaitCancelled.TrySetResult(true);
            throw;
        }
    }

    /// <summary>
    /// 记录当前请求的方法、路径、查询和内容类型。
    /// </summary>
    private void CaptureRequest()
    {
        var request = _httpContextAccessor.HttpContext!.Request;
        _probe.LastRequest = new CapturedRequest(
            request.Method,
            request.Path.Value ?? string.Empty,
            request.QueryString.Value ?? string.Empty,
            request.ContentType ?? string.Empty);
    }
}

/// <summary>
/// 表示创建订单时提交的数据。
/// </summary>
public sealed class OrderInput
{
    /// <summary>
    /// 获取或设置订单名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置订单数量。
    /// </summary>
    public int Quantity { get; set; }
}

/// <summary>
/// 表示订单文件上传时提交的附加数据。
/// </summary>
public sealed class OrderUploadInput
{
    /// <summary>
    /// 获取或设置文件描述。
    /// </summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// 表示订单端点返回的数据。
/// </summary>
public sealed class OrderDto
{
    /// <summary>
    /// 获取或设置订单标识。
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 获取或设置订单名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置订单数量。
    /// </summary>
    public int Quantity { get; set; }
}

/// <summary>
/// 表示服务端收到的上下文及 SDK 请求头信息。
/// </summary>
public sealed class OrderHeadersDto
{
    /// <summary>
    /// 获取或设置标准租户标识请求头的值。
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置标准关联标识请求头的值。
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置语言请求头的值。
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置宿主默认 SDK 请求头的值。
    /// </summary>
    public string SdkDefault { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置自定义租户请求头的值。
    /// </summary>
    public string CustomTenantId { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置自定义关联标识请求头的值。
    /// </summary>
    public string CustomCorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置自定义语言请求头的值。
    /// </summary>
    public string CustomLanguage { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置请求到达时的主机名。
    /// </summary>
    public string RequestHost { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置第一个 SDK 的默认请求头值。
    /// </summary>
    public string SdkA { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置第二个 SDK 的默认请求头值。
    /// </summary>
    public string SdkB { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置第一个 SDK 消息处理器添加的请求头值。
    /// </summary>
    public string SdkHandlerA { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置第二个 SDK 消息处理器添加的请求头值。
    /// </summary>
    public string SdkHandlerB { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置备用 SDK 的租户请求头值。
    /// </summary>
    public string AlternateTenantId { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置备用 SDK 的关联标识请求头值。
    /// </summary>
    public string AlternateCorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置备用 SDK 的语言请求头值。
    /// </summary>
    public string AlternateLanguage { get; set; } = string.Empty;
}

/// <summary>
/// 保存由测试服务端捕获的 HTTP 请求信息。
/// </summary>
public sealed class CapturedRequest
{
    /// <summary>
    /// 初始化 CapturedRequest 类的新实例。
    /// </summary>
    /// <param name="method">HTTP 请求方法。</param>
    /// <param name="path">请求路径。</param>
    /// <param name="query">请求查询字符串。</param>
    /// <param name="contentType">请求内容类型。</param>
    public CapturedRequest(string method, string path, string query, string contentType)
    {
        Method = method;
        Path = path;
        Query = query;
        ContentType = contentType;
    }

    /// <summary>
    /// 获取 HTTP 请求方法。
    /// </summary>
    public string Method { get; }

    /// <summary>
    /// 获取请求路径。
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// 获取请求查询字符串。
    /// </summary>
    public string Query { get; }

    /// <summary>
    /// 获取请求内容类型。
    /// </summary>
    public string ContentType { get; }
}

/// <summary>
/// 为端到端测试记录请求并协调并发和取消场景。
/// </summary>
public sealed class WebApiClientTestProbe
{
    /// <summary>
    /// 同步并发请求计数及屏障状态的访问。
    /// </summary>
    private readonly object _concurrentRequestsLock = new();

    /// <summary>
    /// 当前并发屏障要求到达的请求数。
    /// </summary>
    private int _concurrentRequestTarget;

    /// <summary>
    /// 已到达当前并发屏障的请求数。
    /// </summary>
    private int _concurrentRequestsArrived;

    /// <summary>
    /// 并发请求全部到达时完成的任务源。
    /// </summary>
    private TaskCompletionSource<bool>? _concurrentRequestsReached;

    /// <summary>
    /// 获取或设置最近一次捕获的 HTTP 请求。
    /// </summary>
    public CapturedRequest LastRequest { get; set; } = new(string.Empty, string.Empty, string.Empty, string.Empty);

    /// <summary>
    /// 获取用于通知等待端点已开始的任务源。
    /// </summary>
    public TaskCompletionSource<bool> WaitStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// 获取用于通知等待端点已收到取消的任务源。
    /// </summary>
    public TaskCompletionSource<bool> WaitCancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// 设置并发请求屏障的目标数量并重置到达状态。
    /// </summary>
    /// <param name="count">放行屏障所需的请求数量。</param>
    public void ExpectConcurrentRequests(int count)
    {
        lock (_concurrentRequestsLock)
        {
            _concurrentRequestTarget = count;
            _concurrentRequestsArrived = 0;
            _concurrentRequestsReached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    /// <summary>
    /// 清除并发请求屏障及其计数状态。
    /// </summary>
    public void ResetConcurrentRequests()
    {
        lock (_concurrentRequestsLock)
        {
            _concurrentRequestTarget = 0;
            _concurrentRequestsArrived = 0;
            _concurrentRequestsReached = null;
        }
    }

    /// <summary>
    /// 等待并发请求全部到达屏障。
    /// </summary>
    /// <param name="cancellationToken">用于取消等待的令牌。</param>
    /// <returns>表示屏障放行的异步操作。</returns>
    public Task WaitForConcurrentRequestsAsync(CancellationToken cancellationToken)
    {
        Task waitTask;
        lock (_concurrentRequestsLock)
        {
            if (_concurrentRequestTarget == 0)
                return Task.CompletedTask;

            _concurrentRequestsArrived++;
            if (_concurrentRequestsArrived >= _concurrentRequestTarget)
                _concurrentRequestsReached!.TrySetResult(true);

            waitTask = _concurrentRequestsReached!.Task;
        }

        return waitTask.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
    }
}

/// <summary>
/// 管理 WebApiClient 端到端测试使用的 TestServer 生命周期。
/// </summary>
public sealed class WebApiClientTestServerFixture : IAsyncLifetime
{
    /// <summary>
    /// 当前启动的测试宿主；初始化完成前为空。
    /// </summary>
    private IHost? _server;

    /// <summary>
    /// 获取已启动的测试宿主；尚未初始化时抛出异常。
    /// </summary>
    public IHost Server => _server ?? throw new InvalidOperationException("TestServer has not been initialized.");

    /// <summary>
    /// 获取用于记录请求并协调测试的探针。
    /// </summary>
    public WebApiClientTestProbe Probe { get; } = new();

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        _server = new HostBuilder()
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddHttpContextAccessor();
                    services.AddControllers(options => options.AddBing(services));
                    services.AddSingleton(Probe);
                    services.AddScoped<IOrdersAppService, OrdersAppService>();
                    services.AddAuthentication("sdk-test")
                        .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("sdk-test", _ => { });
                    services.AddAuthorization();
                    services.AddBingDynamicApi(typeof(OrdersAppService).Assembly, options =>
                        options.TypePredicate = type => type == typeof(OrdersAppService));
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                }))
            .Build();

        await _server.StartAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (_server != null)
        {
            await _server.StopAsync();
            _server.Dispose();
            _server = null;
        }
    }
}

/// <summary>
/// 使用异步本地状态提供当前 WebApiClient 请求上下文。
/// </summary>
public sealed class TestRequestContextAccessor : IBingWebApiClientRequestContextAccessor
{
    /// <summary>
    /// 保存当前异步执行流的请求上下文。
    /// </summary>
    private readonly AsyncLocal<BingWebApiClientRequestContext?> _current = new();

    /// <summary>
    /// 获取或设置当前异步执行流的请求上下文。
    /// </summary>
    public BingWebApiClientRequestContext? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }

    /// <inheritdoc />
    public BingWebApiClientRequestContext? GetCurrentContext() => _current.Value;
}

/// <summary>
/// 保存认证处理器使用的测试凭据及请求状态。
/// </summary>
public sealed class TestCredentialState
{
    /// <summary>
    /// 获取或设置宿主认证处理器使用的访问令牌。
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// 获取或设置关联的 HttpClient 名称。
    /// </summary>
    public string HttpClientName { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置最近一次请求的目标地址。
    /// </summary>
    public Uri? LastRequestUri { get; set; }
}

/// <summary>
/// 按测试凭据为 SDK 请求添加 Bearer 认证信息。
/// </summary>
public sealed class TestAuthorizationMessageHandler : DelegatingHandler
{
    /// <summary>
    /// 获取共享的测试凭据状态。
    /// </summary>
    private readonly TestCredentialState _credentialState;

    /// <summary>
    /// 初始化 TestAuthorizationMessageHandler 类的新实例。
    /// </summary>
    /// <param name="credentialState">提供访问令牌和请求记录状态的对象。</param>
    public TestAuthorizationMessageHandler(TestCredentialState credentialState) => _credentialState = credentialState;

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _credentialState.LastRequestUri = request.RequestUri;
        if (!string.IsNullOrWhiteSpace(_credentialState.Token))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _credentialState.Token);

        return base.SendAsync(request, cancellationToken);
    }
}

/// <summary>
/// 为第一个订单 SDK 的请求添加标记请求头。
/// </summary>
public sealed class OrdersSdkMarkerHandler : DelegatingHandler
{
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Add("X-Sdk-Handler-A", "handler-a");
        return base.SendAsync(request, cancellationToken);
    }
}

/// <summary>
/// 为备用订单 SDK 的请求添加标记请求头。
/// </summary>
public sealed class AlternateOrdersSdkMarkerHandler : DelegatingHandler
{
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Add("X-Sdk-Handler-B", "handler-b");
        return base.SendAsync(request, cancellationToken);
    }
}

/// <summary>
/// 验证测试 SDK Bearer 令牌并创建认证结果。
/// </summary>
public sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
#if NET8_0_OR_GREATER
    /// <summary>
    /// 初始化 TestAuthenticationHandler 类的新实例。
    /// </summary>
    /// <param name="options">认证方案选项监视器。</param>
    /// <param name="logger">认证处理器日志工厂。</param>
    /// <param name="encoder">URL 编码器。</param>
    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }
#else
    /// <summary>
    /// 初始化 TestAuthenticationHandler 类的新实例。
    /// </summary>
    /// <param name="options">认证方案选项监视器。</param>
    /// <param name="logger">认证处理器日志工厂。</param>
    /// <param name="encoder">URL 编码器。</param>
    /// <param name="clock">用于读取系统时间的时钟。</param>
    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock)
        : base(options, logger, encoder, clock)
    {
    }
#endif

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!string.Equals(Request.Headers.Authorization.ToString(), "Bearer sdk-secret", StringComparison.Ordinal))
            return Task.FromResult(AuthenticateResult.NoResult());

        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "sdk-client") }, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>
/// 表示用于验证注册拒绝具体类型契约的非接口类型。
/// </summary>
public sealed class ConcreteSdkContract
{
}

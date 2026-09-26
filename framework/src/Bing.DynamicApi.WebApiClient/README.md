# Bing.DynamicApi.WebApiClient

面向第三方 SDK 的 WebApiClientCore 声明式客户端集成。SDK 接口和 DTO 属于调用方自己的发布契约，不需要继承 `IAppService`，也不会运行时读取动态 API 描述。现有 `Bing.DynamicApi.Client` 仍用于根据运行时描述创建客户端代理；两个客户端包互不替代。

## 注册与宿主配置

```csharp
services.AddBingWebApiClient<IOrdersSdk>(options =>
    options.BaseAddress = new Uri(configuration["OrdersApi:BaseAddress"]!))
    .ConfigureHttpClient(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Add("X-Sdk-Version", "1.0");
    })
    .AddHttpMessageHandler<OrdersAuthenticationHandler>();
```

`AddBingWebApiClient<TApi>` 只注册一次 WebApiClientCore typed API，并返回该 API 对应的 `IHttpClientBuilder`。可在返回的 builder 上配置 `HttpClient`、认证、日志和其他处理器；不会额外为同一接口注册第二个 `HttpClient`。`BaseAddress` 是 `HttpApiOptions.HttpHost` 的回退值：宿主可以在调用本扩展前通过 WebApiClientCore 的 options 配置 `HttpHost`，该值会保留；宿主也可在返回的 builder 上用 `ConfigureHttpApi` 覆盖它。builder 上的 `ConfigureHttpClient` 和默认请求头配置照常保留。

```csharp
services.ConfigureHttpApi<IOrdersSdk>(options =>
    options.HttpHost = new Uri(configuration["OrdersApi:BaseAddress"]!));
services.AddBingWebApiClient<IOrdersSdk>(options =>
    options.BaseAddress = new Uri("https://fallback.example/"));
```

注册动态上下文访问器后，客户端会在每次请求发送时读取当前租户、关联 ID 和语言，并写入默认的 `X-Tenant-Id`、`X-Correlation-Id`、`Accept-Language` 请求头。宿主可以通过选项更改头名称，或设为 `null` 禁用单项透传。

请求发送时，本包会先移除当前配置管理的同名请求头，再写入当前上下文中的非空值。因此上下文为空或某个值为空时，不会继续发送 `HttpClient.DefaultRequestHeaders` 中可能残留的旧租户、关联 ID 或语言值。若宿主需要固定请求头兜底，请使用不同的头名称或独立的请求处理器；将某个头名称设为 `null` 后，该头不由本包管理。

```csharp
services.AddHttpContextAccessor();
services.AddSingleton<IBingWebApiClientRequestContextAccessor, OrdersRequestContextAccessor>();
services.AddTransient<OrdersAuthenticationHandler>();

public sealed class OrdersRequestContextAccessor : IBingWebApiClientRequestContextAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public OrdersRequestContextAccessor(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    public BingWebApiClientRequestContext? GetCurrentContext()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
            return null;

        return new BingWebApiClientRequestContext(
            context.Request.Headers["X-Tenant-Id"].ToString(),
            context.TraceIdentifier,
            context.Request.Headers["Accept-Language"].ToString());
    }
}
```

自定义访问器由请求处理器在发送时调用。用于读取请求作用域数据时，应通过线程安全的 ambient accessor（例如 `IHttpContextAccessor`）获取当前请求，而不要把某次请求的上下文值缓存在单例字段中。

同一宿主可以注册多个独立的 SDK 接口。每个接口拥有自己的地址回退值、上下文头名称和 builder 配置，但可以共享同一个上下文访问器：

```csharp
services.AddSingleton<IBingWebApiClientRequestContextAccessor, RequestContextAccessor>();

services.AddBingWebApiClient<IOrdersSdk>(options =>
{
    options.BaseAddress = new Uri(configuration["OrdersApi:BaseAddress"]!);
});

services.AddBingWebApiClient<IInventorySdk>(options =>
{
    options.BaseAddress = new Uri(configuration["InventoryApi:BaseAddress"]!);
    options.TenantHeaderName = "X-Inventory-Tenant";
    options.CorrelationIdHeaderName = "X-Inventory-Correlation";
    options.LanguageHeaderName = "X-Inventory-Language";
});
```

## 声明 SDK 契约

对外接口显式声明 HTTP 动词、最终路由、参数来源及序列化方式。路由中的 `v1` 是 SDK 发布契约的一部分；服务端路由或版本变化时，需要同步发布接口并运行契约测试。

```csharp
public interface IOrdersSdk
{
    [HttpGet("api/app/v1/orders/{id}")]
    [JsonReturn]
    Task<OrderDto> GetAsync(
        [PathQuery] Guid id,
        [PathQuery] string term,
        CancellationToken cancellationToken = default);

    [HttpPost("api/app/v1/orders")]
    [JsonReturn]
    Task<OrderDto> CreateAsync(
        [JsonContent] OrderInput input,
        CancellationToken cancellationToken = default);

    [HttpPost("api/app/v1/orders/upload")]
    [RawReturn]
    Task<string> UploadAsync(
        [FormDataContent] OrderUploadInput metadata,
        FormDataFile file,
        CancellationToken cancellationToken = default);

    [HttpGet("api/app/v1/orders/download")]
    [RawReturn]
    Task<HttpResponseMessage> DownloadAsync(CancellationToken cancellationToken = default);
}
```

`OrderDto`、`OrderInput` 和 `OrderUploadInput` 由第三方 SDK 自己定义。普通 JSON 使用 `[JsonReturn]` / `[JsonContent]`；原样文本或响应对象使用 `[RawReturn]`。WebApiClientCore multipart 表单通过 `[FormDataContent]` 表单参数和 `FormDataFile` 文件参数组合声明。

## 流和错误的所有权

`RawReturn` 返回 `HttpResponseMessage` 时由调用者负责释放响应及其内容：

```csharp
using var response = await orders.DownloadAsync(cancellationToken);
response.EnsureSuccessStatusCode();
await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
await stream.CopyToAsync(destination, cancellationToken);
```

本包不转换 WebApiClientCore 异常。当前依赖版本的非成功状态通过 `HttpRequestException` 公开，状态细节可从其 `ApiResponseStatusException` 内层异常读取；异常中的响应消息应在不再使用时释放。取消令牌触发时，当前版本也会以 `HttpRequestException` 公开，内层为 `TaskCanceledException`。该异常模型与运行时客户端的 `BingRemoteCallException` 不同。

包不默认启用重试。宿主若配置重试处理器，应只对自身确认可重放的请求启用，并考虑文件流、请求体和取消语义。

## 版本和扩展

包目标框架为 `net6.0`、`net8.0`，并固定依赖 `WebApiClientCore` 2.1.6。认证、日志、JSON 序列化和请求处理可继续通过 WebApiClientCore 配置 API、`IHttpClientBuilder`、处理器和过滤器扩展；本包只增加动态上下文头透传，不另行实现代理、元数据缓存、重试或远程错误包装。

# Bing.DynamicApi.Client

面向 `IAppService` 契约的运行时 HTTP 客户端代理。该组件依赖 Bing 的动态 API 描述契约，不依赖 ABP。

```csharp
services.AddBingDynamicApiClient(options =>
{
    options.AddRemoteService("orders", remote =>
    {
        remote.BaseUrl = configuration["Services:Orders"];
        remote.ApiVersion = "v1";
        remote.MaxRetries = 0;
    });
});
services.AddBingDynamicApiClient<IOrderAppService>("orders");
```

调用端注入共享服务接口即可：

```csharp
public sealed class OrderConsumer(IOrderAppService orders)
{
    public Task<OrderDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        orders.GetAsync(id, cancellationToken);
}
```

代理首次调用时请求 `{BaseUrl}/api/dynamic-api/{ApiVersion}/definition`，并按远程服务名称和 API 版本缓存描述。请求使用 `IHttpClientFactory`，支持 `Task` / `Task<T>`、路径与查询参数、JSON 请求体、multipart 文件、`IRemoteStreamContent` 下载、取消令牌和 `BingRemoteCallException`。默认重试关闭；配置的重试仅用于可重放的 GET 元数据和业务请求。

宿主可注册 `IBingDynamicApiRequestHandler` 注入凭据或其他请求头，并实现 `IBingDynamicApiRequestContext` 透传租户、关联 ID 和语言。`IBingDynamicApiJsonSerializer`、`IBingDynamicApiParameterConverter` 和 `IBingDynamicApiRetryPolicy` 分别用于替换默认 JSON 序列化、简单参数转换和重试决策。重试默认关闭，且只允许对 GET 或元数据请求重试；宿主策略不能让业务请求体请求变为可重放请求。

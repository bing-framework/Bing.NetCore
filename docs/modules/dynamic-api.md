# 动态 API 模块

> 内容核对：2026-10-13。对应 `Bing.AspNetCore.Mvc`、`Bing.DynamicApi.Contracts`、`Bing.DynamicApi.Client`、`Bing.DynamicApi.WebApiClient`。

## 功能说明

动态 API 模块包含三条互补链路：

1. 服务端扫描应用服务程序集，把符合条件的 `IAppService` 实现按约定注册为 MVC Controller。
2. 运行时客户端读取服务端描述，使用共享服务接口生成 HTTP 代理。
3. WebApiClientCore 客户端用于调用方自行维护路由契约的第三方 SDK，不读取动态描述。

## 核心实现逻辑与关键文件

| 环节 | 关键路径 | 行为 |
| --- | --- | --- |
| 服务发现与 MVC 注册 | `framework/src/Bing.AspNetCore.Mvc/Bing/DynamicApi/` | `AddBingDynamicApi` 扫描程序集、注册 Controller 别名、应用路由与参数绑定约定，并检测重复路由。 |
| 描述协议 | `framework/src/Bing.DynamicApi.Contracts/` | 定义版本、服务、方法和参数描述模型。 |
| 运行时代理 | `framework/src/Bing.DynamicApi.Client/Bing/` | 获取并缓存描述，使用 `DispatchProxy` 组装请求，处理 JSON、查询、路由、表单、流和远程错误。 |
| 声明式 SDK | `framework/src/Bing.DynamicApi.WebApiClient/Bing/` | 在 WebApiClientCore 上增加基础地址配置和租户、关联 ID、语言请求头透传。 |
| 验证 | `framework/tests/Bing.DynamicApi.Client.Tests/`、`framework/tests/Bing.DynamicApi.WebApiClient.Tests/` | 覆盖端到端调用、取消、重试、泛型载荷、文件与上下文头。 |

服务端默认路由为 `api/app/v1/{service}/{method}`；服务描述端点固定为 `GET /api/dynamic-api/{version}/definition`。服务名、方法路由、版本和是否把版本放进路由均可配置。

## 对外接口与用法

服务端：

```csharp
services.AddScoped<IOrderAppService, OrderAppService>();
services.AddBingDynamicApi(typeof(OrderAppService).Assembly, options =>
{
    options.ApiVersion = "v1";
    options.TypePredicate = type => type == typeof(OrderAppService);
});
```

运行时客户端：

```csharp
services.AddBingDynamicApiClient(options =>
{
    options.AddRemoteService("orders", remote =>
    {
        remote.BaseUrl = configuration["Services:Orders"];
        remote.ApiVersion = "v1";
    });
});
services.AddBingDynamicApiClient<IOrderAppService>("orders");
```

声明式 SDK：

```csharp
services.AddBingWebApiClient<IOrdersSdk>(options =>
    options.BaseAddress = new Uri(configuration["OrdersApi:BaseAddress"]));
```

## 依赖与配置项

| 项 | 说明 |
| --- | --- |
| 服务端 | `Bing.AspNetCore.Mvc`；应用服务契约需继承 `IAppService`，可用 `DisableBingDynamicApiAttribute` 排除。 |
| 运行时客户端 | `Bing.DynamicApi.Client`，依赖契约包、`IHttpClientFactory` 和共享服务接口。 |
| 声明式客户端 | `Bing.DynamicApi.WebApiClient`，目标框架 `net6.0;net8.0`，依赖 WebApiClientCore 2.1.6。 |
| 远程配置 | `BaseUrl` 必须是绝对 URI；`ApiVersion` 不能为空；`MaxRetries` 默认 `0`。 |
| 上下文头 | 默认 `X-Tenant-Id`、`X-Correlation-Id`、`Accept-Language`，名称设为 `null` 可禁用单项透传。 |
| 扩展点 | `IBingDynamicApiRequestHandler`、`IBingDynamicApiRequestContext`、`IBingDynamicApiJsonSerializer`、`IBingDynamicApiParameterConverter`、`IBingDynamicApiRetryPolicy`。 |

## 已知限制与注意事项

- 运行时客户端接口方法必须返回 `Task` 或 `Task<T>`；同步返回、`ValueTask` 为待实现。
- 单个方法不支持多个 JSON 请求体，也不支持 JSON 请求体与表单参数混用。
- 文件上传参数必须实现 `IRemoteStreamContent`；流结果也必须与接口返回类型匹配。
- 自动重试默认关闭，只允许用于可重放的 GET 或描述请求；文件流和普通请求体不会被强行重放。
- WebApiClientCore 路径不会读取动态描述，异常模型也不同：它不包装为 `BingRemoteCallException`。
- 服务端路由冲突会在 MVC 模型构建阶段失败；显式路由与约定路由混用时应运行端到端契约测试。
- 动态描述目前是运行时客户端协议，不是 OpenAPI 的替代品；OpenAPI 互操作能力为待确认。

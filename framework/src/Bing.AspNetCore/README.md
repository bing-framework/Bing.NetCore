# Bing.AspNetCore
[![NuGet](https://img.shields.io/nuget/v/Bing.AspNetCore.svg)](https://www.nuget.org/packages/Bing.AspNetCore/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.AspNetCore.svg)](https://www.nuget.org/packages/Bing.AspNetCore/) ![TFM](https://img.shields.io/badge/TFM-net6.0-blue.svg)

> ASP.NET Core 集成：模块化管道、中间件栈、`ApiResult` 与控制器基类。
>
> **目标框架**：`net6.0` ｜ **分层**：L6 表现层 ｜ **当前版本**：7.0.0 ｜ **源文件**：79
>
> **上游 Bing 包**：`Bing.AspNetCore.Abstractions`、`Bing.Auditing`、`Bing.Logging`、`Bing.ExceptionHandling`、`Bing.Security`

---

## 这是什么

Web 宿主的核心包。提供 `AspNetCoreModule` / `AspNetCoreBingModule` 基类、`UseBing()` 管道初始化、统一响应 `ApiResult`、异常处理中间件 `BingExceptionHandlingMiddleware`，以及 `BingControllerBase` / `ApiControllerBase`。

## 安装

```bash
dotnet add package Bing.AspNetCore
```

## 快速上手

```csharp
// Startup.ConfigureServices
services.AddBing().AddModule<AppModule>();

// Startup.Configure
app.UseBing();

// 控制器
public class OrderController : ApiControllerBase
{
    [HttpGet("{id}")]
    public async Task<IActionResult> GetAsync(Guid id)
        => Success(await _service.GetAsync(id));      // 自动包装为 ApiResult
}
```

新应用可使用依赖图入口：

```csharp
services.AddBingApplication<AppModule>();

// Startup.Configure
app.UseBing();
```

异步模块使用 `app.UseBingAsync()`。框架会把 `IApplicationBuilder` 放入 `BingModuleInitializationContext.HostContext`；实现 `IBingAsyncModuleInitializer` 的模块可以从上下文读取 `ServiceProvider` 和 Web 宿主对象，在 `InitializeAsync` 中完成异步初始化。

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| `UseBing(this IApplicationBuilder)` | `Microsoft.AspNetCore.Builder` | Web 初始化：遍历模块执行 `UseModule(app)` |
| `UseBingAsync(this IApplicationBuilder, CancellationToken)` | `Microsoft.AspNetCore.Builder` | 异步初始化模块，并将 `IApplicationBuilder` 作为 `HostContext` |
| `UseBingExceptionHandling()` | `Microsoft.AspNetCore.Builder` | 异常处理中间件 |
| `AddBingHttpAuditing(...)` / `UseBingHttpAuditing()` | `Bing.AspNetCore.Auditing` | 采集 HTTP 元数据并保存审计日志；不读取请求正文 |
| `AddBingTrafficGate(...)` / `UseBingTrafficGate()` | `Bing.AspNetCore.Hosting` | 提供 `/health/ready`、拒绝排空期间新请求并等待在途请求 |
| `AddBingLocalRateLimiting(...)` / `UseBingRateLimiting()` | `Bing.AspNetCore.RateLimiting` | 按策略、稳定路由、租户和主体执行令牌桶限流 |
| `UseCorrelationId` / `UseRealIp(...)` / `UseRequestResponseLog(app)` | `Bing.AspNetCore.Extensions` | 链路标识 / 真实 IP / 请求响应日志 |


### HTTP 流量闸门

```csharp
services.AddBingTrafficGate(options =>
{
    options.ReadinessPath = "/health/ready";
    options.DrainTimeout = TimeSpan.FromSeconds(30);
});

app.UseBingTrafficGate();
```

闸门使用原子状态和在途计数：排空后新请求返回 503，健康检查返回非 200。宿主停止等待超时会报告 `TimeoutException`，调用方取消会报告 `OperationCanceledException`，并记录在途请求数；Generic Host 可能以聚合异常传播停止错误。等待结束不会释放仍在执行的请求，也不代表清理完成。调用方应先确认活动操作结束，再释放根容器。

闸门计数只覆盖下游 HTTP 管道，不代表请求作用域异步释放、`OnCompleted` 回调、脱离请求的任务、CAP 消费者或定时任务已经完成；它们需要分别排空。排空提前完成会取消本次等待的剩余计时器和取消注册。

### HTTP 审计

```csharp
services.AddBingHttpAuditing(options =>
{
    options.ApplicationName = "orders";
    options.MaxUrlLength = 2048;
    options.UserIdClaimType = "sub";
    options.TenantClaimType = "tenant_id";
    // 可选诊断预算；默认无限等待，超时后仍等待实际保存完成。
    options.SaveTimeout = TimeSpan.FromSeconds(5);
    // 仅允许已确认不含敏感值的查询字段；默认不记录查询参数。
    options.AllowedQueryParameters.Add("page");
});

app.UseRouting();
app.UseAuthentication();
app.UseBingHttpAuditing();
```

中间件默认只记录路径，查询参数需显式加入白名单；不读取请求正文。身份只从已认证的声明读取，用户、名称、租户和客户端声明类型均可配置。`IAuditingManager` 由请求作用域注入，业务代码可通过同一管理器向 `Current` 添加操作，并发请求相互隔离。`DisableAuditingAttribute` 可用于端点级关闭。

审计保存失败默认只输出异常类型和受限关联标识，不输出 URL 或异常正文，并保留原始业务异常。`ThrowOnStoreFailure` 可传播没有原始业务异常时的保存失败，但保存发生在业务执行后，不能撤回已发送的响应或已提交的事务。`BingHttpAuditingOptions.SaveTimeout` 可设置保存等待预算；预算耗尽只记录诊断，仍会等待实际保存完成后再释放作用域，不会取消保存或提前释放连接。关键审计应使用[数据库事务审计入口](../../../docs/guides/auditing-persistence.md)，不能用 HTTP 保存后抛错代替回滚。

`SaveTimeout` 默认是 `Timeout.InfiniteTimeSpan`。有限预算到期时，中间件收集 `TimeoutException`，继续等待同一个保存任务结束，再释放审计作用域；日志和故障传播在清理完成后执行。该选项提供超时诊断，不是请求耗时上限，也不会取消存储或提前释放连接。没有原始业务异常且 `ThrowOnStoreFailure = true` 时，即使迟到保存最终成功，仍会传播超时；有原始业务异常时保留它，并将超时及其他清理异常附于 `Data["Bing.HttpAuditCleanupErrors"]`。宿主停止时仍需等待活动保存完成。

手工创建中间件的消费者需要迁移：构造函数不再接收管理器，`InvokeAsync(HttpContext, IAuditingManager)` 接收当前请求管理器。常规 `UseBingHttpAuditing()` 注册用法保持不变。

### HTTP 限流

```csharp
services.AddBingLocalRateLimiting(100, 10, TimeSpan.FromMinutes(10), 10000,
    options =>
    {
        options.PolicyName = "api";
        options.UserIdClaimType = "sub";
        options.TenantClaimType = "tenant_id";
    });

app.UseRouting();
app.UseAuthentication();
app.UseBingRateLimiting();
```

限流中间件只读取已经认证的用户和租户声明。`UserIdClaimType` 默认是 `ClaimTypes.NameIdentifier`，JWT 保留原始声明时可配置为 `sub`；中间件应位于认证和路由之后。默认使用匹配的路由模板，其次端点名称；未匹配端点的请求共享 `unmatched` 分桶，避免随机 URL 持续增加键。`RouteKeySelector` 可显式指定稳定路由键。

匿名分桶使用 `Connection.RemoteIpAddress`，不会直接采信转发头。若使用 `UseRealIp`，必须在 `RealIpOptions.TrustedProxies` 中配置真实代理地址；未配置时中间件会忽略转发头。代理部署也可在管道前部配置 ASP.NET Core `ForwardedHeaders` 的 `KnownProxies` 或 `KnownNetworks`，不要把客户端可伪造的转发头当作身份或限流安全边界。过长组合键使用长度受限摘要，摘要只限制单键长度，不减少不同身份的键数量；容量仍由限流器的 `maxKeys` 控制。

`HttpContext.GetRemoteIpAddress()` 同样只读取经过 ASP.NET Core 处理后的 `Connection.RemoteIpAddress`，不会直接信任请求头中的 `X-Forwarded-For`。需要使用代理转发地址时，必须先配置受信的 `ForwardedHeaders` 中间件。

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `ApiResult` | `Bing.AspNetCore.Mvc` | 统一响应：`Code` / `Message` / `Data` / `OperationTime` |
| `StatusCode` | `Bing.AspNetCore.Mvc` | `Ok=1` / `Fail=2` / `Unauthorized=401` |
| `BingControllerBase` / `ApiControllerBase` | `Bing.AspNetCore.Mvc` | 控制器基类 |

## 与其它包的关系

**本包依赖**：`Bing.AspNetCore.Abstractions`、`Bing.Auditing`、`Bing.Logging`、`Bing.ExceptionHandling`、`Bing.Security`

`BingControllerBase` 使用 `Bing.Core` 中的中立 `[Autowired]` 标记。本包不再传递
`Bing.Aop.AspectCore`；需要属性注入时，宿主应另外引用适配器并调用
`services.EnableAutowired()` 或 `services.EnableAop()`。

**依赖本包**：`Bing.AspNetCore.MultiTenancy`

Web 应用停止前应等待 `ShutdownBingAsync()` 完成，再释放根服务提供程序。使用 Generic Host 时，Bing 注册的 hosted service 会在 Host 停止阶段自动调用模块关闭流程；若应用自行管理容器，必须显式调用 `provider.ShutdownBingAsync()`。模块只释放自己拥有的资源，控制器、日志和其他 DI 服务由容器释放。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.AspNetCore

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。

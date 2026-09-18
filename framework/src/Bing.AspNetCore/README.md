# Bing.AspNetCore
[![NuGet](https://img.shields.io/nuget/v/Bing.AspNetCore.svg)](https://www.nuget.org/packages/Bing.AspNetCore/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.AspNetCore.svg)](https://www.nuget.org/packages/Bing.AspNetCore/) ![TFM](https://img.shields.io/badge/TFM-net6.0-blue.svg)

> ASP.NET Core 集成：模块化管道、中间件栈、`ApiResult` 与控制器基类。
>
> **目标框架**：`net6.0` ｜ **分层**：L6 表现层 ｜ **当前版本**：7.0.0 ｜ **源文件**：79
>
> **上游 Bing 包**：`Bing.AspNetCore.Abstractions`、`Bing.Logging`、`Bing.ExceptionHandling`、`Bing.Security`、`Bing.Aop.AspectCore`

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

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| `UseBing(this IApplicationBuilder)` | `Microsoft.AspNetCore.Builder` | Web 初始化：遍历模块执行 `UseModule(app)` |
| `UseBingExceptionHandling()` | `Microsoft.AspNetCore.Builder` | 异常处理中间件 |
| `UseCorrelationId` / `UseRealIp(...)` / `UseRequestResponseLog(app)` | `Bing.AspNetCore.Extensions` | 链路标识 / 真实 IP / 请求响应日志 |

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `ApiResult` | `Bing.AspNetCore.Mvc` | 统一响应：`Code` / `Message` / `Data` / `OperationTime` |
| `StatusCode` | `Bing.AspNetCore.Mvc` | `Ok=1` / `Fail=2` / `Unauthorized=401` |
| `BingControllerBase` / `ApiControllerBase` | `Bing.AspNetCore.Mvc` | 控制器基类 |

## 与其它包的关系

**本包依赖**：`Bing.AspNetCore.Abstractions`、`Bing.Logging`、`Bing.ExceptionHandling`、`Bing.Security`、`Bing.Aop.AspectCore`

**依赖本包**：`Bing.AspNetCore.MultiTenancy`

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.AspNetCore

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。

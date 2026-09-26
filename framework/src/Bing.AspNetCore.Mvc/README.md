# Bing.AspNetCore.Mvc
[![NuGet](https://img.shields.io/nuget/v/Bing.AspNetCore.Mvc.svg)](https://www.nuget.org/packages/Bing.AspNetCore.Mvc/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.AspNetCore.Mvc.svg)](https://www.nuget.org/packages/Bing.AspNetCore.Mvc/) ![TFM](https://img.shields.io/badge/TFM-net6.0-blue.svg)

> MVC 控制器基类、全局结果过滤与 MVC 选项扩展。
>
> **目标框架**：`net6.0` ｜ **分层**：L6 表现层 ｜ **当前版本**：7.0.0 ｜ **源文件**：16
>
> **上游 Bing 包**：无（本包是叶子/基础包）

---

## 这是什么

提供 `ResultHandlerAttribute`（自动把返回值包装成 `ApiResult`）、`QueryControllerBase` / `CrudControllerBase`、`MvcOptions.AddBing(services)`，以及显式启用的应用服务动态 API。

## 安装

```bash
dotnet add package Bing.AspNetCore.Mvc
```

## 快速上手

```csharp
services.AddControllers(o => o.AddBing(services));
services.AddBingDynamicApi(typeof(OrderAppService).Assembly);
```

`AddBingDynamicApi` 只扫描传入程序集中的具体 `IAppService`，未调用时现有 MVC API 行为不变。默认服务根路径为 `/api/app/v1`；例如 `IOrderAppService.GetAsync(Guid id)` 映射到 `GET /api/app/v1/order/{id}`。简单参数进入查询字符串，路由模板中的参数进入路径，复杂参数进入 JSON 请求体；`IRemoteStreamContent` 使用 multipart 上传和流式下载。显式 HTTP 路由特性优先于方法名前缀约定。

可以按类型筛选服务并调整根路径或版本：

```csharp
services.AddBingDynamicApi(typeof(OrderAppService).Assembly, options =>
{
    options.RoutePrefix = "api/app";
    options.ApiVersion = "v1";
    options.TypePredicate = type => type.Namespace == "MyApp.Services";
});
```

在应用服务类或方法上标记 `[DisableBingDynamicApi]` 可排除动态端点。运行时描述位于 `/api/dynamic-api/{version}/definition`，根据宿主的 MVC 授权配置访问，不会被强制设为匿名。动态端点保留 MVC 的直接响应和 HTTP 状态，不经过 `ResultHandlerAttribute`；显式 Controller 的既有包装行为不变。

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| `MvcOptions.AddBing(this MvcOptions, IServiceCollection)` | `Bing.AspNetCore.Mvc` | 注入模型绑定器、`IRemoteStreamContent` 元数据与输出格式化器 |
| `IServiceCollection.AddBingDynamicApi(Assembly, Action<BingDynamicApiOptions>)` | `Bing.AspNetCore.Mvc.DynamicApi` | 按约定注册应用服务控制器和版本化 API 描述 |

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `ResultHandlerAttribute` | `Bing.AspNetCore.Mvc.Filters` | 结果自动包装 |
| `BingExceptionFilter` | `Bing.AspNetCore.Mvc.ExceptionHandling` | MVC 异常过滤 |
| `QueryControllerBase` / `CrudControllerBase` | `Bing.AspNetCore.Mvc` | 查询 / CRUD 控制器基类 |
| `BingDynamicApiOptions` / `DisableBingDynamicApiAttribute` | `Bing.AspNetCore.Mvc.DynamicApi` | 动态 API 路由、版本、筛选和排除配置 |

## 与其它包的关系

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.AspNetCore.Mvc

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。

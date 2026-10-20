# Web、MVC 与统一响应

## 功能说明

`Bing.AspNetCore.Abstractions` 提供 Web 契约；`Bing.AspNetCore` 负责宿主集成、控制器基类及中间件；`Bing.AspNetCore.Mvc.Contracts`、`Bing.AspNetCore.Mvc` 和 `Bing.AspNetCore.Mvc.UI` 分别提供 MVC 契约、应用服务控制器集成和 Razor 支持。动态 API 的客户端与描述协议见[独立模块文档](dynamic-api.md)。

## 核心实现逻辑与关键文件

| 路径 | 职责 |
| --- | --- |
| `framework/src/Bing.AspNetCore/` | 模块初始化、异常处理中间件、请求上下文、控制器和 `ApiResult`。 |
| `framework/src/Bing.AspNetCore.Mvc/` | MVC 约定、应用服务集成和动态 Controller。 |
| `framework/src/Bing.AspNetCore.Mvc.Contracts/` | MVC 层公共契约。 |
| `framework/src/Bing.AspNetCore.Mvc.UI/` | Razor 相关注册与 UI 扩展。 |

宿主先注册模块与 MVC 服务，再由 `UseBing` 执行模块的管道装配。控制器辅助方法返回统一响应，异常处理中间件承担异常到 HTTP 响应的转换；并非任意手写响应都会自动包装。

## 对外接口与用法

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;

services.AddControllers();
services.AddBing().AddModule<AppModule>();
// AppModule 为业务宿主已定义的模块。
app.UseBing();
```

控制器可继承 `ApiControllerBase`，通过 `Success` 等辅助方法返回 `ApiResult`；UI 包入口是 `AddRazorHtml`。完整宿主配置见[使用文档](../getting-started/使用文档.md)。

## 依赖与配置项

Web 工程主要面向 `net6.0`，依赖 ASP.NET Core、核心模块、日志、安全和异常处理组件。认证需要单独配置 JWT 与 ASP.NET Core 认证/授权管道；请求日志、关联标识、真实 IP 与 MVC Options 分别由对应组件装配。

## 已知限制与注意事项

- 中间件顺序由宿主模块和启动代码决定，注册某个包不会自动建立全部管道。
- 统一业务响应码与 HTTP 状态码是两种概念；调用方应同时检查需要的字段。
- 内置请求响应日志没有通用自动脱敏保证。
- 自动 OpenAPI 互操作与生产部署验收中无法从代码确认的行为统一标为“待实现/待确认”。

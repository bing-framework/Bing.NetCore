# Web、安全与多租户

> **适用版本**：框架 `7.0.0`｜源码核对：2026-10-13

## 功能说明

Web 层提供 MVC 约定、统一响应与异常处理、JWT 认证、当前用户、真实 IP、关联标识、请求审计和多租户解析。`Bing.Permissions` 提供用户、角色等 Identity 模型，但不是完整的通用权限授权框架。

## 核心实现

- Web 基础设施：`framework/src/Bing.AspNetCore/`
- MVC 与契约：`framework/src/Bing.AspNetCore.Mvc/`、`Bing.AspNetCore.Mvc.Contracts/`
- JWT：`framework/src/Bing.AspNetCore.Authentication.JwtBearer/`
- 当前用户与声明：`framework/src/Bing.Security/`
- 权限模型：`framework/src/Bing.Permissions/`
- 多租户：`framework/src/Bing.MultiTenancy*/`、`Bing.AspNetCore.MultiTenancy/`

## 用法

```csharp
services.AddJwt(configuration);
services.Configure<BingTenantResolveOptions>(options =>
{
    options.TenantResolvers.Add(new CurrentUserTenantResolveContributor());
    options.TenantResolvers.Add(new HeaderTenantResolveContributor());
});

app.UseAuthentication();
app.UseBingClaimsMap();
app.UseMiddleware<MultiTenancyMiddleware>();
app.UseAuthorization();
```

多租户包没有 `AddMultiTenancy` 或 `UseMultiTenancy` 快捷入口。相关服务可由 Bing 约定扫描注册，也可由宿主显式注册；宿主必须配置解析贡献者，并用 ASP.NET Core 原生 `UseMiddleware<MultiTenancyMiddleware>()` 接入请求管道。实际命名空间和中间件顺序见[使用文档](../getting-started/使用文档.md)及[安全指南](../operations/安全指南.md)。

## 依赖与配置

- JWT 读取 `JwtOptions` 配置节，生产环境必须提供独立密钥、签发者和受众。
- 真实 IP 仅信任 `RealIpOptions.TrustedProxies` 中的连接对端。
- 多租户可从当前用户、请求头、查询、路由、域名或 Cookie 解析，具体贡献者由宿主配置。

## 已知限制与注意事项

- 自定义 JWT 校验路径与标准 JwtBearer 的有效期行为不同；启用异常抛出模式前必须核对过期检查。
- 多租户解析只确定“当前租户”，不会自动为三套 ORM 注入所有查询过滤。
- **待实现**：通用 `IPermissionChecker`、全局权限过滤、XSS、CSRF 和 IP 白名单。
- 请求/响应日志没有通用字段级脱敏，敏感端点应排除或由宿主实现脱敏。

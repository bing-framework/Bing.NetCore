# 认证、权限与多租户

## 功能说明

`Bing.Security` 提供当前用户与安全基础设施，JWT 包提供令牌与 Web 认证，`Bing.Permissions` 提供权限管理；多租户包负责当前租户上下文与 Web 租户解析。`Bing.Authorization.Abstractions` 是授权抽象，安装契约包不等于配置完整鉴权。

## 核心实现逻辑与关键文件

| 路径 | 职责 |
| --- | --- |
| `framework/src/Bing.Security/`、`framework/src/Bing.Permissions/` | 用户上下文、安全与权限模型。 |
| `framework/src/Bing.AspNetCore.Authentication.JwtBearer/Bing/` | JWT Options、构建/存储/校验和认证注册。 |
| `framework/src/Bing.MultiTenancy/Bing/MultiTenancy/` | 基于执行上下文的当前租户与临时切换。 |
| `framework/src/Bing.MultiTenancy.Abstractions/`、`framework/src/Bing.AspNetCore.MultiTenancy/` | 租户契约、HTTP 解析贡献者与中间件。 |

JWT 注册绑定 `JwtOptions` 并添加 `jwt` 策略。租户解析贡献者从不同请求来源取租户信息，`CurrentTenant.Change` 返回可释放的恢复作用域；它不会自动改写查询为租户过滤 SQL。

## 对外接口与用法

```csharp
using Bing.AspNetCore.Authorization.JwtBearer;

services.AddJwt(configuration);
```

已注入的 `ICurrentTenant` 可以用 `using (currentTenant.Change(tenantId))` 临时切换上下文；释放后恢复父级租户。权限策略、认证/授权中间件与数据过滤由宿主显式接入。

## 依赖与配置项

JWT 至少配置 `JwtOptions` 中的 `Secret`、`Issuer`、`Audience` 与令牌时长。多租户使用 `BingTenantResolveOptions` 配置解析贡献者；请求来源、租户存储和业务权限实现需与宿主一致。密钥和外部连接配置通过宿主配置源提供。

## 已知限制与注意事项

- 当前租户解析不等同于数据隔离；仓储/SQL 查询需要显式过滤。
- 框架没有统一 `AddMultiTenancy` / `UseMultiTenancy` 入口，也没有默认覆盖所有业务接口的权限过滤器。
- 标准 JWT 中间件和自定义令牌校验器行为不同，不能共用一套过期假设，详见[安全指南](../operations/安全指南.md)。
- 参考宿主被注释的 Identity/权限注册及生产就绪性：待实现/待确认。

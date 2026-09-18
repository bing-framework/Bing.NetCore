# 配方 4：授权与 RBAC

> **适用版本**：框架 `7.0.0`（`version.props`）｜ 目标框架：类库 `netstandard2.0`、Web / 应用 `net6.0`｜ 内容核对：2026-09-18｜ 版本历史见 [发行说明](../../ReleaseNotes.md)

> 目标：让 API **默认拒绝、显式放行**，并把用户/角色/权限的模型落到位。
>
> ⚠ **框架层没有全局权限过滤器**，也没有 `IPermissionChecker`。本配方里「默认拒绝」这层要你自己加——照抄参考实现的约定即可，很短。

## 1. 装包

```bash
dotnet add package Bing.AspNetCore.Authentication.JwtBearer   # JWT 认证 + "jwt" 策略
dotnet add package Bing.Security                               # ICurrentUser 等
dotnet add package Bing.Permissions                            # 用户/角色模型与登录管理
```

## 2. 第一步：全局「默认拒绝」约定

参考实现 `modules/admin`（EF 版与 FreeSQL 版**都有**）的做法：用一个 Controller 约定给**所有**控制器加授权过滤器。

```csharp
internal class AuthorizeControllerModelConvention : IControllerModelConvention
{
    public void Apply(ControllerModel controller)
        => controller.Filters.Add(new AuthorizeFilter("jwt"));
}
```

```csharp
services.AddControllers(o =>
{
    o.Filters.Add<ResultHandlerAttribute>();      // 统一响应
    o.Filters.Add<BingExceptionFilter>();        // 统一异常
    o.Conventions.Add(new AuthorizeControllerModelConvention());   // ← 默认拒绝
})
.AddControllersAsServices();                    // 属性注入需要
```

需要放行的控制器/方法，再显式标注：

```csharp
[AllowAnonymous]                 // 登录接口
public Task<TokenDto> Login(LoginDto dto) => ...;
```

> 这套约定的价值在于**「新增 Controller 默认就是受保护的」**，不会有人忘记加 `[Authorize]`。

## 3. 第二步：JWT 注册

```csharp
services.AddJwt(Configuration);   // 读配置节 JwtOptions
```

```json
{
  "JwtOptions": {
    "Secret": "从环境变量或用户机密读取，不要写进配置文件",
    "Issuer": "换成你自己的值",
    "Audience": "换成你自己的值",
    "AccessExpireMinutes": 30,
    "RefreshExpireMinutes": 43200
  }
}
```

| 事实 | 值 |
| --- | --- |
| 密钥为空 | **抛异常**（不会静默退化到无签名）✅ |
| 标准中间件校验 | `ValidateIssuerSigningKey/Issuer/Audience/Lifetime` 全为 `true`，`ClockSkew` 30 秒 |
| 默认签发者 / 受众 | `bing_identity` / `bing_client`——**生产务必替换** |
| Access Token 默认有效期 | 5 分钟（`AccessExpireMinutes` 未配置时的兜底） |
| Refresh Token 默认有效期 | 7 天（10080 分钟） |

🔴 **一条必须知道的坑**：`JwtOptions.ThrowEnabled = true` 会让授权走 `ThrowExceptionHandleAsync` 分支，而该分支**既不校验 `exp`/`nbf`，也没有 `IsExpired()` 调用**——过期校验会静默消失。默认（`false`）走 `ResultHandleAsync`，靠服务端令牌存储的 `IsExpired()` 兜底，是**有**过期校验的。详见 [安全指南 §2](../../operations/安全指南.md)。

## 4. 第三步：权限模型

`Bing.Permissions` 提供 Identity 侧的模型与管理入口（如 `ISignInManager<TUser, TKey>`、`UserBase<TUser, TKey>`），但**校验逻辑需要你自己接**——框架没有 `IPermissionChecker`。

典型落地：

```csharp
public class PermissionAppService : AppServiceBase
{
    // 1) 加载当前用户拥有的权限码集合（缓存到 Redis / 内存）
    // 2) 暴露给前端做菜单与按钮级控制
}
```

后端接口级校验，用 ASP.NET Core 原生授权：

```csharp
[Authorize(Policy = "Permission:Order.Create")]
public Task<Guid> Create(OrderCreateDto dto) => ...;

// 注册策略
services.AddAuthorization(o =>
    o.AddPolicy("Permission:Order.Create", p =>
        p.RequireAssertion(ctx => /* 从 claims 或缓存里判断权限码 */)));
```

## 5. 取当前用户

```csharp
public class OrderAppService : AppServiceBase
{
    private readonly ICurrentUser _currentUser;   // Bing.Security
}
```

## 注意事项

1. 🔴 **框架没有全局权限过滤器**：第 2 步的「默认拒绝」约定是你自己加的，别以为装了包就有了。
2. **生产替换 `Issuer` / `Audience` 默认值**，并把 `Secret` 移出配置文件（见 [安全指南 §1](../../operations/安全指南.md)）。
3. **`UseRealIp()` 不是安全边界**：它只从 `x-forwarded-for` 解析真实 IP，**不做任何放行/拒绝**。
4. **框架没有速率限制、CSRF、XSS 防护**，需要的话请在应用层或网关层实现。
5. `Bing.Permissions` 是 `net6.0`，`Bing.Security` 是 `netstandard2.0`。

## 相关

- [安全指南 §2 / §5](../../operations/安全指南.md)
- [最佳实践 §9](../最佳实践.md)
- [包索引](../../getting-started/包索引.md)

# Bing.AspNetCore.Authentication.JwtBearer
[![NuGet](https://img.shields.io/nuget/v/Bing.AspNetCore.Authentication.JwtBearer.svg)](https://www.nuget.org/packages/Bing.AspNetCore.Authentication.JwtBearer/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.AspNetCore.Authentication.JwtBearer.svg)](https://www.nuget.org/packages/Bing.AspNetCore.Authentication.JwtBearer/) ![TFM](https://img.shields.io/badge/TFM-net6.0-blue.svg)

> JWT 认证：`AddJwt`，配置节 `JwtOptions`，策略名 `jwt`。
>
> **目标框架**：`net6.0` ｜ **分层**：L6 表现层 ｜ **当前版本**：7.0.0 ｜ **源文件**：25
>
> **上游 Bing 包**：`Bing.AspNetCore.Abstractions`、`Bing.Caching`

---

## 这是什么

一行接入 JWT：读取 `JwtOptions` 配置节，注册 Token 构建/校验/存储，并添加名为 `jwt` 的授权策略。

## 安装

```bash
dotnet add package Bing.AspNetCore.Authentication.JwtBearer
```

## 快速上手

```csharp
services.AddJwt(Configuration);

// appsettings.json
// "JwtOptions": {
//   "Secret": "...", "Issuer": "...", "Audience": "...",
//   "AccessExpireMinutes": 120, "RefreshExpireMinutes": 10080
// }
```

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| `AddJwt(IConfiguration)` | `Bing.AspNetCore.Authorization.JwtBearer.Extensions` | 配置节 **`JwtOptions`**，策略名 **`jwt`** |
| `AddBingJwtBearer(this AuthenticationBuilder, ...)` | `Microsoft.Extensions.DependencyInjection` | 细粒度方案注册（4 个重载） |
| `UseJwtCustomerAuthorize(app, ...)` | `Bing.AspNetCore` | 自定义授权中间件（含匿名路径） |

## 与其它包的关系

**本包依赖**：`Bing.AspNetCore.Abstractions`、`Bing.Caching`

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.AspNetCore.Authentication.JwtBearer

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。

# ADR-0006：多租户解析链

> **适用版本**：框架 `7.0.0`（`version.props`）｜ 目标框架：类库 `netstandard2.0`、Web / 应用 `net6.0`｜ 内容核对：2026-09-18｜ 版本历史见 [发行说明](../../ReleaseNotes.md)

| | |
| --- | --- |
| **状态** | 🟢 已接受（代码已落地） |
| **日期** | 2026-09-18（事后补录） |
| **涉及** | `Bing.MultiTenancy.Abstractions`（`ITenantResolver` / `ITenantResolveContributor` / `ICurrentTenant`）、`Bing.AspNetCore.MultiTenancy` |

## 背景

租户标识从哪里来，在不同系统里答案完全不同：SaaS 后台看当前登录用户、网关转发看 header、老系统迁移看 query string、多域名站点看 domain、还有用 cookie 的。框架不能替用户拍板"你该用哪种"，但也不应让用户为每个组合写一遍解析逻辑。

## 备选方案

| 方案 | 优点 | 缺点 |
| --- | --- | --- |
| **A. 只支持一种**（如 header） | 极简 | 现实中不成立，用户必然要改源码 |
| **B. 一个配置项选一种**（`'TenantSource': 'Header'`） | 简单直观 | 无法组合多来源优先级；加一种要改配置枚举 |
| **C. 可组合的 Contributor 链**（本方案） | 策略可插拔、可排序、可自定义；用户可只注册需要的 | 需要理解"链"的概念；配置比单个字符串啰嗦 |
| **D. 中间件里写死优先级** | 零配置 | 不可扩展，用户改不了顺序 |

## 决策

采用 **方案 C**：

```
ITenantResolveContributor 链：CurrentUser → Header / QueryString / Route / Domain / Cookie
```

`MultiTenancyMiddleware` 用 `ICurrentTenant.Change(id, name)` 在请求内切换（基于 `AsyncLocal` 作用域隔离）。

**刻意决定不提供 `UseMultiTenancy` 便捷扩展**——要求使用者手动：

```csharp
app.UseMiddleware<MultiTenancyMiddleware>();
services.Configure<BingTenantResolveOptions>(o => o.TenantResolvers.Add<...>());
```

理由是「不过度封装」：租户解析是安全敏感的**准入逻辑**，让它显式出现在 `Startup` 里比藏在一个 `UseXxx()` 后面更好——使用者必须知道自己启用了什么。

## 后果

**正面**

- 解析策略可组合、顺序可调、可完全自定义；
- `AsyncLocal` 作用域隔离，多线程/异步场景下不会串租户；
- 因为要求显式装配，使用者天然知道系统启用了哪些解析方式。

**负面**

- **没有便捷入口**，每个项目都要写注册代码；对新手不友好。
- ⚠ **更关键的是：解析链只解决"租户是谁"，不解决"数据过滤"**。ORM 侧**没有任何一支**提供开箱即用的租户查询过滤（EF 支要自定义 `FilterBase<T>`，Dapper 支要注入 `ISqlTenantFilterContributor`，FreeSQL 支完全没有）。漏配过滤 = **跨租户数据泄露**，见 [能力矩阵 §8](../../getting-started/能力矩阵.md) 与 [安全指南](../../operations/安全指南.md)。
- 链的顺序写错会导致优先级错误（如 Cookie 排在 CurrentUser 之前），且症状隐蔽。

## 相关文档

- [能力矩阵 §2 / §8](../../getting-started/能力矩阵.md)（三支 ORM 的多租户支持现状）
- [安全指南](../../operations/安全指南.md)（多租户隔离）
- [使用文档 §7](../../getting-started/使用文档.md)

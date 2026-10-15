# 通用横切服务

> **适用版本**：框架 `7.0.0`｜源码核对：2026-10-13

## 功能说明

本组包含缓存、日志、邮件、本地化、对象映射、文本模板、验证和诊断适配。多数能力通过独立包按需引入，不由一个总开关统一启用。

## 核心实现

- 缓存：`framework/src/Bing.Caching*`
- 日志与接收器：`framework/src/Bing.Logging*`
- 邮件：`framework/src/Bing.Emailing/`、`Bing.MailKit/`
- 本地化：`framework/src/Bing.Localization*`
- 映射：`framework/src/Bing.ObjectMapping/`、`Bing.AutoMapper/`
- 模板：`framework/src/Bing.TextTemplating*`
- 验证：`framework/src/Bing.Validation*`
- 诊断：`Bing.Extensions.SkyApm.Diagnostics.Sql`、`Bing.MiniProfiler`

## 用法

```csharp
services.AddAutoMapper();
services.AddJsonLocalization("Resources");
services.AddMailKit(options => { /* SMTP 与安全连接配置 */ });
```

缓存实现、邮件的两个相似入口、日志装配和文本模板手工注册见[横切能力文档](../guides/横切能力文档.md)。

## 依赖与配置

- CSRedis/FreeRedis 缓存和 Redis 锁要求宿主初始化对应客户端。
- MailKit 与 `System.Net.Mail` 是两套实现，`AddMailKit` 在不同命名空间存在相似重载。
- Serilog、Exceptionless、SkyAPM 和 MiniProfiler 需要各自第三方包与宿主配置。
- 文本模板没有统一 `AddXxx` 入口，需要按接口手工装配渲染器和定义提供程序。

## 已知限制与注意事项

- `Bing.EasyCaching` 的注册扩展仍被注释，标记为**待实现**，不要仅因工程存在就视为可用。
- 邮件队列只有存取能力，后台调度由宿主或 Hangfire 等执行。
- HTTP 请求日志缺少通用字段级脱敏。
- 参数验证默认不启用；AspectCore 宿主需显式调用 `EnableBingParameterValidation()`。


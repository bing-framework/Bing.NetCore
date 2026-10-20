# 邮件、模板、本地化、锁与业务组件

## 功能说明

本组包括 `Bing.Emailing` / `Bing.MailKit` 邮件发送、文本模板与 Scriban 引擎、本地化抽象与实现、CSRedis 分布式锁，以及 `Bing.Biz`、支付与 OAuth 登录组件。它们按需装配，没有共享的万能注册入口。

## 核心实现逻辑与关键文件

| 路径 | 职责 |
| --- | --- |
| `framework/src/Bing.Emailing/`、`framework/src/Bing.MailKit/` | 邮件配置提供器、发送器、附件与队列服务。 |
| `framework/src/Bing.TextTemplating/`、`framework/src/Bing.TextTemplating.Scriban/` | 模板定义、内容贡献者、渲染器与 Scriban 引擎。 |
| `framework/src/Bing.Localization/`、`framework/src/Bing.Localization.Abstractions/` | 本地化契约与资源实现。 |
| `framework/src/Bing.Locks.CSRedis/` | `IDistributedLock` 的 Redis 实现与注册。 |
| `components/src/Bing.Biz/`、`components/src/Bing.Biz.Payments/`、`components/src/Bing.Biz.OAuthLogin/` | 业务值类型、支付工厂/回调与各平台授权提供器。 |

邮件和支付工厂从配置提供器取得渠道配置；模板渲染先解析定义与内容，再选择引擎。本地化按资源和语言解析；分布式锁依赖 CSRedis 静态客户端，与新缓存管理器的显式客户端注入不同。

## 对外接口与用法

```csharp
using Bing.Locks;

// 宿主已完成 RedisHelper.Initialization(redisClient)。
services.AddRedisDistributedLock();
```

MailKit 注册方法是 `Bing.MailKit.Extensions` 下的 `AddMailKit`；SMTP 实现也有同名方法，应明确命名空间。支付通过 `Bing.Biz.Payments.Extensions.AddPay` 配置，再用 `IPayFactory.CreatePayService` 选择渠道。模板需配置 `BingTextTemplatingOptions.RenderingEngines` 与模板定义；完整示例见[横切能力](../guides/横切能力文档.md)和[组件库](../guides/组件库文档.md)。

## 依赖与配置项

邮件需要 SMTP/MailKit 连接及发件配置；模板依赖 Scriban 和业务内容贡献者；锁依赖 CSRedisCore。支付/OAuth 需要渠道应用、回调地址和签名配置，实际依赖版本由组件工程导入的 `.props` 管理。不要把业务密钥复制到使用示例。

## 已知限制与注意事项

- 模板、本地化、授权抽象等契约不保证单独装包即可运行。
- `AddPay` 系列返回 `void`，不能链式调用。
- 微信退款服务与支付工厂未支持渠道、OAuth 统一工厂：待实现/待确认；以具体源码分支为准。
- 仓库包含旧第三方协议实现，现行支付和 OAuth 平台兼容性：待实现/待确认。
- Redis 锁、缓存命名空间及邮件队列分别管理生命周期，不提供跨系统事务。

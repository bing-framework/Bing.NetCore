# ADR-0007：横切能力靠约定自动注册（而非统一 AddXxx）

> **适用版本**：框架 `7.0.0`（`version.props`）｜ 目标框架：类库 `netstandard2.0`、Web / 应用 `net6.0`｜ 内容核对：2026-09-18｜ 版本历史见 [发行说明](../../ReleaseNotes.md)

| | |
| --- | --- |
| **状态** | 🟢 已接受（代码已落地） |
| **日期** | 2026-09-18（事后补录） |
| **涉及** | `Bing.Core`、`Bing.Logging`、`Bing.Caching.*`、`Bing.Emailing`、`Bing.TextTemplating`、`Bing.Locks.*`、`Bing.ObjectMapping` 等 |

## 背景

按 ADR-0002 的约定式 DI，绝大多数服务其实**不需要** `AddXxx()` 就能注册。于是一个设计问题浮现：要不要仍然为每个能力提供一个 `AddXxx()` 入口，只为给用户一个"明确的调用点"？

## 备选方案

| 方案 | 优点 | 缺点 |
| --- | --- | --- |
| **A. 每个能力都提供 `AddXxx()`** | 入口显式、可发现性好（IDE 提示） | 大量空壳方法；用户每次都要猜名字；与约定式 DI 重复，两处都可能成为"真相来源" |
| **B. 一律靠约定注册**（本方案，少数例外） | 与 ADR-0002 一致；零样板；不存在"注册了但没加 AddXxx 导致不生效"的不一致 | 新用户找不到入口；某些能力**确实需要**启动期初始化参数，纯约定表达不了 |
| **C. 全靠约定，且命名统一**（如统一加不影响语义的前缀） | 折中 | 仍然绕不开"哪些有入口哪些没有"的困惑；命名统一反而牺牲语义（`EnableAop` 比 `AddAopSupport` 更准确） |

## 决策

采用 **方案 B**：默认靠 `ITransientDependency` / `IScopedDependency` 自动注册 + `Options` 类配置。

**保留显式入口的例外**（确实需要启动期初始化或有强副作用）：

| 入口 | 说明 |
| --- | --- |
| `AddBingLogging` | 日志管线初始化 |
| `AddDefaultEventBus` / `AddCapEventBus` | 事件总线**二选一**，必须显式 |
| `AddSmtpEmail` / `AddMailKit` | 邮件发送器（两套实现，必须挑一个） |
| `AddRedisDistributedLock` | 分布式锁需要连接配置 |
| `AddAutoMapper` | 映射 Profile 扫描 |
| `AddJwt` | 认证方案注册 |
| `EnableAop` | 启用 AOP（**不是 `AddAop`**） |

## 后果

**正面**

- 与 ADR-0002 语义一致，框架只有**一套**注册心智模型；
- 消除了"约定注册"与"显式注册"谁才是真相来源的二义性；
- 少了大量空壳扩展方法，包 API 面更小。

**负面**

- **可发现性最差的一项决策**。典型抱怨包括：
  - "缓存怎么没有 `AddCache`？"（答案：要 `RedisHelper.Initialization(...)` 后手工 `AddScoped<ICache, CSRedisCacheManager>()`）
  - "文本模板怎么注册？"（答案：**没有任何 `IServiceCollection` 扩展**，必须手动装配 `BingTextTemplatingOptions`）
  - "AOP 为什么是 `EnableAop` 不是 `AddAop`？"
- 入口名抽查后发现若干不对称：`Exceptionless` 的入口在 **`Serilog` 命名空间**（`WriteTo.Exceptionless`）、`SkyApm` 的入口是 `AddSqlQuery(this SkyApmExtensions)`、`AddPay` **返回 `void` 不能链式**——三者都不在 `IServiceCollection` 上。

**缓解措施**

- [架构文档 §12](../架构文档.md)：注册入口**完整清单**（含命名空间与全部重载）+ §12.7 防误用清单；
- [使用文档 §3.3](../../getting-started/使用文档.md)：精简速查；
- [组件库文档](../../guides/组件库文档.md)：边缘包逐个标注状态徽章（✅ / 🔌 / 🧩 / ⚠）。

## 相关文档

- [架构文档 §12 注册入口完整清单](../架构文档.md)
- [组件库文档](../../guides/组件库文档.md)
- [横切能力文档](../../guides/横切能力文档.md)

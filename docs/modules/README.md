# 模块文档索引

> 内容核对：2026-10-13。这里收录跨多个 NuGet 包、需要统一理解的功能模块；单包 API 仍以各工程目录的 `README.md` 和源码为准。

| 模块 | 一句话简介 | 状态 |
| --- | --- | --- |
| [核心启动与依赖注入](core.md) | 模块发现、依赖加载、约定注册、AOP 与异常基础设施。 | 已实现；排序与加载边界已列明 |
| [Web 与 MVC](web.md) | ASP.NET Core 启动、统一响应、异常处理与 Razor 扩展。 | 已实现；需要宿主装配 |
| [动态 API](dynamic-api.md) | 将 `IAppService` 约定公开为 HTTP API，并提供运行时代理与 WebApiClientCore 两种客户端。 | 已实现；限制已列明 |
| [领域与工作单元](domain.md) | DDD、应用服务、审计、验证、提交与对象映射。 | 已实现；全局规则及自动事件接入待实现/待确认 |
| [数据访问](data-access.md) | EF Core、FreeSQL、Dapper、SQL Builder 与各数据库 Provider。 | 已实现；异步与表达式限制已列明 |
| [缓存](caching.md) | 统一缓存抽象、Redis 适配器、EasyCaching、命名空间、拓扑、分片和 Hybrid 协调。 | 已实现；部分能力依赖 Provider |
| [事件与消息](events.md) | 进程内发布、CAP 消息及领域事件分发的边界。 | 已实现；自动排空待实现/待确认 |
| [安全与多租户](security.md) | JWT、授权、权限、安全服务及租户解析和切换。 | 已实现；数据过滤由业务装配 |
| [日志与诊断](observability.md) | Bing 日志、Serilog、Exceptionless、SkyAPM 与 MiniProfiler。 | 已实现；外部平台验收待实现/待确认 |
| [外围集成](integrations.md) | 邮件、模板、本地化、锁、支付与 OAuth 的注册和限制。 | 部分实现；退款与工厂待实现/待确认 |
| [工程与测试](development.md) | 分析器、生成器、构建、测试和文档校验工具。 | 已实现；API 成员页待实现/待确认 |
| [参考宿主](reference-apps.md) | admin 分层参考实现及 WebAPI、Hangfire、WinForms 示例。 | 已实现；生产就绪性待实现/待确认 |

更细的专题文档：

- 核心模块、依赖注入与启动流程：[架构文档](../architecture/架构文档.md)
- DDD、工作单元与仓储：[领域建模指南](../guides/领域建模指南.md)、[工作单元、仓储与应用服务](../guides/工作单元仓储与应用服务.md)
- EF Core、FreeSQL、Dapper 与 SQL 引擎：[能力矩阵](../getting-started/能力矩阵.md)、[SQL 专项](../sql/README.md)
- 事件与消息：[事件与消息文档](../architecture/事件与消息文档.md)
- 安全、多租户、日志与其他横切能力：[安全指南](../operations/安全指南.md)、[横切能力文档](../guides/横切能力文档.md)
- 业务组件与边缘集成：[组件库文档](../guides/组件库文档.md)

完整覆盖状态见[文档与代码对照清单](../operations/文档与代码对照清单.md)。

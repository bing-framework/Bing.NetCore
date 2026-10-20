# 模块文档索引

> **适用版本**：框架 `7.0.0`｜目标框架以各项目文件为准｜源码核对：2026-10-13

本目录以当前生产源码为准，回答“能力在哪里、怎样启用、边界是什么”。历史任务记录和测试执行报告只作为证据，不作为功能定义；单包 API 仍以各工程 README 和源码为准。

## 状态说明

| 状态 | 含义 |
| --- | --- |
| 已实现 | 存在生产入口与核心实现，可按文档启用 |
| 有限实现 | 主路径可用，但存在明确的容器、部署或一致性边界 |
| 待确认 | 代码或模板存在，但缺少当前环境下的完整运行证据 |
| 待实现 | 未发现可用生产实现 |

## 模块入口

| 模块 | 状态 | 文档 |
| --- | --- | --- |
| 模块图、生命周期与插件 | 已实现 | [模块化与插件](modularity-and-plugins.md) |
| 依赖注入、属性注入与 AOP | 有限实现 | [依赖注入与 AOP](dependency-injection-and-aop.md) |
| 本地事件、领域事件、CAP 与有序消费 | 有限实现 | [事件与一致性](events-and-consistency.md) |
| 审计作用域、HTTP 采集与持久化 | 已实现 | [审计](auditing.md) |
| 本地/Redis 锁与令牌桶限流 | 有限实现 | [锁与限流](locking-and-rate-limiting.md) |
| EF Core、FreeSQL、工作单元与库存 | 有限实现 | [数据访问与工作单元](data-access-and-unit-of-work.md) |
| Web、JWT、安全与多租户 | 有限实现 | [Web、安全与多租户](web-security-and-multitenancy.md) |
| 缓存、日志、邮件、本地化、映射与模板 | 有限实现 | [通用横切服务](cross-cutting-services.md) |
| 支付/OAuth、分析器、Admin、Samples 与部署资产 | 待确认 | [组件、样例与部署资产](components-samples-and-tooling.md) |
| 宿主角色、流量排空与蓝绿模板 | 待确认 | [宿主与部署](hosting-and-deployment.md) |

## 核对入口

- [文档与源码核对报告](../architecture/documentation-audit.md)：现有文档的有效性、过时项和待确认项。
- [功能与文档对照清单](../architecture/feature-documentation-matrix.md)：按职责域列出实现状态和文档覆盖情况。
- [功能完成度与已知缺口](../architecture/功能完成度.md)：尚未完成或具有明显限制的能力。
- [能力矩阵](../getting-started/能力矩阵.md)：三套数据访问实现的横向选择。
- [迁移指南](../migrations/README.md)：破坏性调整与升级步骤。

| 模块 | 一句话简介 | 状态 |
| --- | --- | --- |
| [核心启动与依赖注入](core.md) | 模块发现、依赖加载、约定注册、AOP 与异常基础设施。 | 已实现；排序与加载边界已列明 |
| [Web 与 MVC](web.md) | ASP.NET Core 启动、统一响应、异常处理与 Razor 扩展。 | 已实现；需要宿主装配 |
| [动态 API](dynamic-api.md) | 将 `IAppService` 约定公开为 HTTP API，并提供运行时代理与 WebApiClientCore 两种客户端。 | 已实现；限制已列明 |
| [领域与工作单元](domain.md) | DDD、应用服务、审计、验证、提交与对象映射。 | 已实现；全局规则及自动事件接入边界见专题文档 |
| [数据访问](data-access.md) | EF Core、FreeSQL、Dapper、SQL Builder 与各数据库 Provider。 | 已实现；异步与表达式限制已列明 |
| [缓存](caching.md) | 统一缓存抽象、Redis 适配器、EasyCaching、命名空间、拓扑、分片和 Hybrid 协调。 | 已实现；部分能力依赖 Provider |
| [事件与消息](events.md) | 进程内发布、CAP 消息及领域事件分发的边界。 | 已实现；可靠跨进程投递使用 CAP |
| [安全与多租户](security.md) | JWT、授权、权限、安全服务及租户解析和切换。 | 已实现；数据过滤由业务装配 |
| [日志与诊断](observability.md) | Bing 日志、Serilog、Exceptionless、SkyAPM 与 MiniProfiler。 | 已实现；外部平台验收待确认 |
| [外围集成](integrations.md) | 邮件、模板、本地化、锁、支付与 OAuth 的注册和限制。 | 部分实现；限制见专题文档 |
| [工程与测试](development.md) | 分析器、生成器、构建、测试和文档校验工具。 | 已实现；API 成员页待生成 |
| [参考宿主](reference-apps.md) | admin 分层参考实现及 WebAPI、Hangfire、WinForms 示例。 | 已实现；生产就绪性待确认 |

更细的专题文档：

- 核心模块、依赖注入与启动流程：[架构文档](../architecture/架构文档.md)
- DDD、工作单元与仓储：[领域建模指南](../guides/领域建模指南.md)、[工作单元、仓储与应用服务](../guides/工作单元仓储与应用服务.md)
- EF Core、FreeSQL、Dapper 与 SQL 引擎：[能力矩阵](../getting-started/能力矩阵.md)、[SQL 专项](../sql/README.md)
- 事件与消息：[事件与消息文档](../architecture/事件与消息文档.md)
- 安全、多租户、日志与其他横切能力：[安全指南](../operations/安全指南.md)、[横切能力文档](../guides/横切能力文档.md)
- 业务组件与边缘集成：[组件库文档](../guides/组件库文档.md)

完整覆盖状态见[文档与代码对照清单](../operations/文档与代码对照清单.md)。

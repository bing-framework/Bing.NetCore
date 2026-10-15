# 文档与源码核对报告

> **适用版本**：框架 `7.0.0`｜核对日期：2026-10-13

## 核对范围

本次逐项比较 `docs/`、`framework/src/`、`modules/`、`samples/`、`deploy/` 和直接测试目录。结论以生产代码入口和实现为主；测试用于证明行为，历史 `ai_docs` 仅用于追溯。

完整的职责域、生产工程和文档状态映射见[功能与文档对照清单](feature-documentation-matrix.md)。

## 已实现并已归档

| 能力 | 代码状态 | 文档入口 |
| --- | --- | --- |
| 模块图、三轮配置、三轮初始化、异步关闭 | 已实现 | [模块化与插件](../modules/modularity-and-plugins.md) |
| 启动期插件和整组热更新 | 已实现；适用范围有限 | [可信插件指南](../guides/modularity-plugins.md) |
| 约定 DI、AspectCore 属性注入与工作单元 AOP | 已实现；容器边界明确 | [依赖注入与 AOP](../modules/dependency-injection-and-aop.md) |
| EF Core/FreeSQL 领域事件派发 | 已实现 | [事件与一致性](../modules/events-and-consistency.md) |
| CAP 元数据、消费幂等和有序事件 | 已实现；需数据库表与业务幂等 | [集群一致性指南](../guides/cluster-consistency.md) |
| 审计管理、HTTP 采集、持久化、归档与清理 | 已实现 | [审计持久化指南](../guides/auditing-persistence.md) |
| 库存条件扣减、请求幂等和订单投影 | 已实现 | [库存订单指南](../guides/inventory-orders.md) |
| 本地/Redis 锁、本地/Redis 令牌桶、HTTP 限流 | 已实现；一致性边界明确 | [锁与限流](../modules/locking-and-rate-limiting.md) |
| 宿主角色、流量闸门和停止排空 | 已实现 | [集群宿主配置](../guides/cluster-host-configuration.md) |

## 已修正的过时结论

- “聚合根领域事件不会自动派发”已过时。EF Core 与 FreeSQL 工作单元现会创建 `DomainEventDispatchSession` 并在事务动作阶段派发。
- “框架没有速率限制”已过时。当前存在本地和 Redis 令牌桶以及 ASP.NET Core 中间件。
- “ADR 只有 10 份”已过时。当前索引包含 ADR-0001 至 ADR-0014。
- FreeSQL 的 65 个异步占位异常已移除；当前限制是通用 `IQueryable` 路径可能同步计算，不应描述为尚未实现。

## 待实现或待确认

| 项目 | 状态 | 原因 |
| --- | --- | --- |
| 不可信插件隔离 | 待实现 | 当前插件均在宿主进程内执行 |
| Web 中间件运行时热替换 | 待实现 | 热更新只覆盖代际服务入口 |
| 动态蓝绿切流、强杀恢复和真实回滚 | 待确认 | 仓库有模板和脚本，仍需目标部署环境验收 |
| 全量 MySQL/CAP/RabbitMQ 场景 | 待确认 | 依赖外部基础设施和对应门控配置 |
| XSS、CSRF、IP 白名单 | 待实现 | 未发现框架级通用实现 |
| 全 ORM 自动多租户过滤 | 待实现 | 当前租户解析不等于数据隔离 |
| RabbitMQ 全局顺序 | 不承诺 | 当前只保证启用有序存储后的同业务流、同实体顺序 |
| 跨 ORM/跨数据库原子事务 | 不支持 | 需出箱、幂等和补偿设计 |

## 文档发布状态

- 根包表已覆盖 70/70 个可发包工程。
- 70 个可发包工程均有包级 README 和 NuGet 徽章；本轮新增 `Bing.Events.EventBusAdapter`、`Bing.PluginRuntime` 的中文说明，并为 `Bing.Ddd.Application.AspectCore` 补充徽章。
- `README.en.md` 和 API 占位页已翻译为中文；文件名为兼容既有链接而保留。
- 仓库文档门禁的 13 项检查全部通过，包括 768 条交叉链接、包表、索引同步、历史坏模式、源码符号和行号锚点。

包级 README 位于对应包项目目录，是 NuGet 包内嵌说明，因此不迁移到 `docs/`；模块级设计与使用说明仍统一放在 `docs/`。

## 文档维护规则

1. 新公共入口先更新对应模块文档，再更新 `docs/toc.yml` 和 `docs/README.md`。
2. 生产实现、测试证据和外部环境验收分开陈述。
3. 未运行的集成测试写“待确认”，不得从模板或单元测试推断生产部署已通过。
4. 删除或迁移能力时保留迁移说明，避免旧类型名继续出现在快速入门中。

## 本次交付清单

### 新增

| 文件 | 摘要 |
| --- | --- |
| `docs/modules/README.md` | 已实现功能模块总索引与状态定义 |
| `docs/modules/modularity-and-plugins.md` | 模块图、生命周期、插件与热更新边界 |
| `docs/modules/dependency-injection-and-aop.md` | 约定 DI、属性注入、AOP 与资源所有权 |
| `docs/modules/events-and-consistency.md` | 本地事件、领域事件、CAP、幂等与顺序消费 |
| `docs/modules/auditing.md` | 审计作用域、HTTP 采集、持久化与留存 |
| `docs/modules/locking-and-rate-limiting.md` | 本地/Redis 锁、租约与令牌桶限流 |
| `docs/modules/data-access-and-unit-of-work.md` | EF Core、FreeSQL、工作单元与库存一致性 |
| `docs/modules/web-security-and-multitenancy.md` | Web、JWT、当前用户、真实 IP 与多租户 |
| `docs/modules/cross-cutting-services.md` | 缓存、日志、邮件、本地化、映射与模板 |
| `docs/modules/components-samples-and-tooling.md` | 组件、分析器、Admin、Samples 与部署资产 |
| `docs/modules/hosting-and-deployment.md` | 宿主角色、流量排空与蓝绿发布边界 |
| `docs/architecture/feature-documentation-matrix.md` | 生产职责域与文档状态逐项对照 |
| `docs/architecture/documentation-audit.md` | 本次源码核对、差异和交付证据 |
| `docs/guides/modularity-di-resources.md` | 模块 DI、循环依赖与同步/异步释放指南 |
| `docs/guides/cluster-consistency.md` | 集群事件、一致性、幂等与并发治理 |
| `docs/guides/cluster-host-configuration.md` | Web/消费者角色和集群宿主配置 |
| `docs/guides/ordered-events.md` | 有序入箱、游标、缺口恢复和留存 |
| `docs/guides/inventory-orders.md` | 库存条件扣减、请求幂等与订单投影 |
| `docs/guides/auditing-persistence.md` | 审计查询、归档、清理与授权边界 |
| `docs/migrations/aspectcore-3.0.md` | AspectCore 3.0 目标框架与兼容迁移 |

### 更新或翻译

| 文件 | 操作 | 摘要 |
| --- | --- | --- |
| `README.md` | 更新 | 修正包数、文档数、包表和文档入口 |
| `docs/README.md` | 更新 | 汇总模块文档、核对报告和专题指南 |
| `docs/README.en.md` | 翻译 | 英文导航翻译为中文，保留文件名兼容链接 |
| `docs/api/index.md` | 翻译 | DocFX 英文占位页改为中文生成说明 |
| `docs/toc.yml` | 更新 | 纳入新增模块、架构、指南和迁移文档 |
| `docs/index.md` | 更新 | 由根 README 同步生成 |
| `docs/CHANGELOG.md` | 更新 | 记录文档核对与新增入口 |
| `docs/architecture/事件与消息文档.md` | 更新 | 修正领域事件、CAP 与派发状态 |
| `docs/architecture/功能完成度.md` | 更新 | 移除已过时的未实现结论 |
| `docs/architecture/子系统深挖.md` | 更新 | 对齐当前事件和资源行为 |
| `docs/getting-started/使用文档.md` | 更新 | 对齐当前注册和运行入口 |
| `docs/getting-started/包索引.md` | 更新 | 包工程由 67 项修正为 70 项 |
| `docs/getting-started/术语表.md` | 更新 | 补充并修正当前模块术语 |
| `docs/guides/modularity-plugins.md` | 更新 | 补充适用版本和插件运行边界 |
| `docs/guides/领域建模指南.md` | 更新 | 修正领域事件派发说明 |
| `docs/guides/工作单元仓储与应用服务.md` | 更新 | 对齐工作单元代理与提交语义 |
| `docs/guides/横切能力文档.md` | 更新 | 对齐中立特性和 AspectCore 执行器 |
| `docs/guides/本地事件与消息事件使用说明.md` | 更新 | 明确单进程和跨节点边界 |
| `docs/migrations/modularity-runtime.md` | 更新 | 对齐附加程序集和当前模块入口 |
| `docs/operations/安全指南.md` | 更新 | 对齐 JWT、真实 IP、多租户和待实现项 |
| `docs/operations/工程化文档.md` | 更新 | 对齐 70 个包级 README 的发布状态 |
| `docs/ai-workflow/TROUBLESHOOTING.md` | 翻译 | 英文标题改为中文 |
| `docs/ai-workflow/PROTOCOL.md` | 翻译 | 英文章节标题改为中文 |
| `docs/sql/migration-guide.md` | 翻译 | 英文章节标题改为中文 |
| `framework/src/Bing.Events.EventBusAdapter/README.md` | 新增 | 旧消息契约到 CAP 的显式适配说明 |
| `framework/src/Bing.PluginRuntime/README.md` | 新增 | 可回收插件代宿主的使用边界 |
| `framework/src/Bing.Ddd.Application.AspectCore/README.md` | 更新 | 补充 NuGet 徽章 |


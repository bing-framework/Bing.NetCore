# 已实现功能模块索引

> **适用版本**：框架 `7.0.0`｜目标框架以各项目文件为准｜源码核对：2026-10-13

本目录以当前生产源码为准，回答“能力在哪里、怎样启用、边界是什么”。历史任务记录和测试执行报告只作为证据，不作为功能定义。

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

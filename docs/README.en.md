# Bing.NetCore 文档精简导航

> **适用版本**：框架 `7.0.0`｜内容翻译与核对：2026-10-13

> 本文件原为英文导航，现已按项目中文文档规范翻译为中文。为避免破坏既有链接，保留 `README.en.md` 文件名。完整入口请优先阅读[文档总导航](README.md)。

## 从这里开始

| 需求 | 文档 | 内容 |
| --- | --- | --- |
| 安装和使用 | [使用文档](getting-started/使用文档.md) | 最小宿主、注册入口、DDD、数据访问、Web 和横切能力 |
| 当前实现概览 | [已实现功能模块索引](modules/README.md) | 按模块查功能、入口、配置和限制 |
| 源码与文档差异 | [功能与文档对照清单](architecture/feature-documentation-matrix.md) | 已实现、文档缺失、描述不符、待实现和待确认项 |
| 理解架构 | [架构文档](architecture/架构文档.md) | 分层、依赖、启动顺序和注册入口 |
| 选择数据访问实现 | [能力矩阵](getting-started/能力矩阵.md) | EF Core、FreeSQL、SQL/Dapper 对照 |
| 从 ABP 迁移 | [ABP 迁移对照](getting-started/abp-migration.md) | 类型映射、行为差异和迁移步骤 |
| 排查问题 | [FAQ 与排错](getting-started/FAQ与排错.md) | 常见错误、原因和处理方式 |

## 按主题查找

| 目录 | 内容 |
| --- | --- |
| `getting-started/` | 使用、能力矩阵、包索引、术语、API 速查、迁移和排错 |
| `architecture/` | 架构、设计依据、事件、功能完成度、源码核对和 ADR |
| `modules/` | 当前已实现功能的模块化说明 |
| `guides/` | 建模、工作单元、插件、事件、审计、库存及配方 |
| `operations/` | 日志、安全、性能、测试、工程化和资产说明 |
| `sql/` | SQL 构建器、Provider、跨库和变更 API |
| `testing/` | 本地与外部数据库集成测试 |
| `migrations/` | 跨版本和破坏性契约迁移 |
| `api/` | docfx 生成的逐成员 API 文档 |

## 重点边界

- 领域事件由 EF Core 和 FreeSQL 工作单元派发，但不提供可靠跨进程投递。
- CAP 是至少一次投递，业务消费者必须幂等。
- 多租户解析不等于数据库行级隔离。
- 插件在进程内执行，仅支持可信代码。
- Redis 锁不能替代数据库条件更新和唯一约束。
- 支付、OAuth、EasyCaching、外部数据库和蓝绿部署仍有待实现或待确认项。

## 仓库级入口

- [贡献指南](../CONTRIBUTING.md)
- [AI 执行规范](../AGENTS.md)
- [样例索引](../samples/README.md)
- [Admin 参考实现](../modules/admin/README.md)
- [文档变更记录](CHANGELOG.md)

项目的权威文档语言为简体中文。类型名、方法名、配置键和命令保持源码原文，以便检索和复制。

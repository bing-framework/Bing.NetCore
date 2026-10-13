# 模块文档索引

> 内容核对：2026-10-13。这里收录跨多个 NuGet 包、需要统一理解的功能模块；单包 API 仍以各工程目录的 `README.md` 和源码为准。

| 模块 | 一句话简介 | 状态 |
| --- | --- | --- |
| [动态 API](dynamic-api.md) | 将 `IAppService` 约定公开为 HTTP API，并提供运行时代理与 WebApiClientCore 两种客户端。 | 已实现；限制已列明 |
| [缓存](caching.md) | 统一缓存抽象、Redis 适配器、EasyCaching、命名空间、拓扑、分片和 Hybrid 协调。 | 已实现；部分能力依赖 Provider |

其他模块已由现有专题文档覆盖：

- 核心模块、依赖注入与启动流程：[架构文档](../architecture/架构文档.md)
- DDD、工作单元与仓储：[领域建模指南](../guides/领域建模指南.md)、[工作单元、仓储与应用服务](../guides/工作单元仓储与应用服务.md)
- EF Core、FreeSQL、Dapper 与 SQL 引擎：[能力矩阵](../getting-started/能力矩阵.md)、[SQL 专项](../sql/README.md)
- 事件与消息：[事件与消息文档](../architecture/事件与消息文档.md)
- 安全、多租户、日志与其他横切能力：[安全指南](../operations/安全指南.md)、[横切能力文档](../guides/横切能力文档.md)
- 业务组件与边缘集成：[组件库文档](../guides/组件库文档.md)

完整覆盖状态见[文档与代码对照清单](../operations/文档与代码对照清单.md)。

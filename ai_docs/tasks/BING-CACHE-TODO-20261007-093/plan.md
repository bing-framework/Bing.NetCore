# BING-CACHE-TODO-20261007-093：Hybrid 失效投递重试、背压与 Redis outbox

## 目标

把当前索引中的 Hybrid 失效消息 `DEFERRED` 项推进为可选、可验证的投递能力，同时保持现有默认行为和失败语义不变。

## 现状和证据

- `HybridInvalidationMessageBatcher` 只负责按序列化字节预算拆分，直接调用 `IEasyCachingBus`。
- `HybridBusFailureTracker` 能把总线发布失败传播给当前缓存操作，但没有持久化待发消息。
- EasyCaching 的 `BusRetryCount` 只覆盖 Provider 内部的当前调用，不能覆盖进程在发布后、调用返回前退出的窗口。
- 当前索引将该事项标为 `DEFERRED`，原因是尚未冻结代理重试、排队上限和崩溃恢复契约。

## 本轮契约

- 新增 `HybridInvalidationDeliveryOptions`，默认 `MaxPendingMessages=0`、`MaxPublishAttempts=1`，默认路径与现有实现完全一致。
- 只有显式配置正的 `MaxPendingMessages` 才启用 Redis outbox；要求分布式 Provider 暴露 `StackExchange.Redis.IDatabase`，否则注册时 fail fast。
- 当前写入/删除操作仍在总线发布最终失败时抛出原始异常；即使失效消息已进入 outbox，也不把当前操作伪装成成功。
- 每条消息按配置进行有限次数重试；重试耗尽后写入 outbox。队列上限同时计算 pending 和 processing，超限不丢弃消息并返回原始发布异常。
- outbox 使用 Redis pending/processing 两个列表；取出后先移动到 processing，发布成功后确认删除。进程在发布和确认之间退出时允许重复发布，失效操作保持幂等，不宣称严格一次。
- 后台投递器在 Redis 连接恢复后继续消费 processing 和 pending；取消或释放只停止本地投递器，不删除未确认消息。
- 本轮不实现消息代理跨产品的统一最大帧协商；网络往返和客户端内部排队观测仍保留为独立 `DEFERRED` 项。

## 变更范围

### P1：配置和投递抽象

- 新增 `HybridInvalidationDeliveryOptions` 及参数校验。
- 新增内部 outbox envelope、Redis outbox 和投递协调器。
- 保留 `HybridInvalidationMessageBatcher` 的现有直接调用测试和兼容路径。

### P2：DI 和 Hybrid 调用链

- `AddHybridCaching`、`AddShardedHybridCaching` 增加可选投递配置入口。
- 每个分布式 Provider 注册一个单例投递器，避免 Scoped Manager 为每个请求创建后台任务。
- `HybridCacheManager` 和客户端分片 Hybrid 路径统一使用投递器。
- 默认配置不创建 outbox，不改变现有注册方式和物理键格式。

### P3：测试和文档

- 增加重试次数、队列满、发布失败入队、成功确认、processing 恢复和取消测试。
- 使用隔离 Redis 验证 pending/processing 列表、队列上限和重建投递器后的恢复；测试受 `RUN_INTEGRATION_TESTS` 门控。
- 更新 EasyCaching README、迁移说明、TODO 索引、执行报告和生产符号映射。

## Change Impact Analysis

- ChangedFiles：Bing.EasyCaching 投递配置、投递器、Redis outbox、注册入口、Hybrid Manager、相关测试和文档。
- ChangedProjects：`Bing.EasyCaching`、`Bing.EasyCaching.Tests`；`Bing.Caching` 公共项目不改。
- ChangedPublicContracts：新增可选配置类型和注册委托；现有方法签名保持兼容。
- ChangedRuntimePaths：Hybrid Redis 写入/删除后的本地失效发布路径。
- ChangedProviders：EasyCaching Redis/Hybrid；Memory、FreeRedis、CSRedis 不变。
- ChangedTFMs：netstandard2.0 生产构建、net6.0/net8.0 测试。
- ChangedBuildPackaging：无新增外部包，复用现有 StackExchange.Redis 和 System.Text.Json 依赖。
- ChangedBenchmarkHarness：无。
- ChangedDocsOnly：README、迁移和 TODO 证据。
- AffectedDependents：Hybrid 单节点、客户端分片 Hybrid、总线故障回归。
- RiskLevel：HIGH；涉及跨进程失效可靠性，但默认关闭且保留原始失败语义。

## 验收标准

- 默认配置的现有 Hybrid 单元/集成行为不变。
- 显式启用时，重试次数和延迟严格受配置限制，队列达到上限时 fail fast。
- Redis outbox 采用 pending -> processing -> ack 顺序，成功发布后才删除；重建投递器能继续处理未确认消息。
- 发布失败不会静默成功；取消不会删除未确认消息。
- 单元测试和本地 Redis 门禁在 net6.0/net8.0 通过，生产 netstandard2.0 构建通过。
- 更新索引时只关闭“Hybrid 失效消息代理背压、重试与 outbox”这一项；网络/队列观测、性能预算和审批项保持原状态。

## 风险和残余

- Redis outbox 与缓存数据共用同一分布式 Provider，但 outbox 列表不是业务事务的一部分；缓存写入成功而入队失败时仍会抛错并要求调用方处理。
- 多个投递器可能重复发布同一 processing 项；失效消息必须保持幂等，不能用于业务事务互斥。
- outbox 只保证至少一次尝试和有界排队，不保证代理严格一次或全局顺序。

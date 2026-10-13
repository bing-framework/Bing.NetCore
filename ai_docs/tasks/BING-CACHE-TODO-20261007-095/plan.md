# BING-CACHE-TODO-20261007-095：Hybrid outbox 毒消息隔离

## 目标

为 Redis Hybrid outbox 增加可选、可验证的毒消息隔离能力，避免单条持续失败消息长期占用 processing 并阻塞后续 pending 消息。默认保持现有无限重试和不丢消息语义。

## 本轮契约

- `MaxDeliveryAttempts=0` 表示不限制同一 outbox 消息跨进程的投递尝试次数，保持 093 之前的默认行为。
- 只有同时设置正的 `MaxDeliveryAttempts` 与 `DeadLetterMaxMessages` 才启用 dead-letter；配置不完整时在注册/构造阶段 fail fast。
- 每次从 pending 移入或从 processing 重试都会在 Redis 中递增持久化尝试次数，进程重启不会清零。
- 达到最大尝试次数后，原子地将消息从 processing 移入有界 dead-letter sorted set，并清理尝试计数；成功隔离后 worker 继续处理后续 pending 消息。
- dead-letter 达到容量上限、转移失败或取消时，消息继续留在 processing，不静默丢弃，也不确认成功。
- dead-letter 只保存原始失效 envelope；本轮不提供自动保留期、人工重放或跨进程全局指标聚合。

## 实现范围

### P1：配置和状态契约

- 在 `HybridInvalidationDeliveryOptions` 增加 `MaxDeliveryAttempts`、`DeadLetterMaxMessages`，默认均为 0，并校验上限及成对启用关系。
- `HybridInvalidationDeliverySnapshot` 增加 durable dead-letter 数量和当前进程隔离计数。
- outbox item 携带当前持久化投递尝试次数。

### P2：Redis outbox 与 worker

- 为每个实例增加同槽 `:attempts` Hash 和 `:dead` Sorted Set。
- `TryTake`/`TryTakeAsync` 递增并返回尝试次数；确认时清理 Hash；dead-letter 转移使用 Lua 原子完成 processing 删除、sorted set 写入和 Hash 清理。
- dead-letter 容量达到上限时返回 false，保留 processing。
- worker 达到阈值后隔离消息并继续 drain；转移失败保持原有阻塞/重试语义。

### P3：测试、文档和索引

- 单元测试覆盖默认关闭、持久化尝试次数、达到阈值后隔离、dead-letter 满时保留 processing。
- 真实 Redis 门禁覆盖 dead-letter 容量和 processing 原子转移，并重建 outbox 验证尝试次数不会清零。
- 更新 README、迁移说明、权威 TODO 索引、执行报告和生产符号映射。

## 不在本轮范围

- dead-letter 保留期、过期清理、空间预算和人工重放。
- 跨进程全局成功/失败/隔离指标聚合。
- 100K 性能矩阵、历史三方基线和外部双分片门禁。

## Change Impact Analysis

- ChangedFiles：EasyCaching outbox 配置、快照、接口、Redis 实现和投递 worker；相关单元/Redis 集成测试、README、迁移说明、TODO 索引和任务证据。
- ChangedProjects：`Bing.EasyCaching`、`Bing.EasyCaching.Tests`。
- ChangedPublicContracts：新增两个可选配置属性和快照字段；缓存操作方法签名不变。
- ChangedRuntimePaths：仅影响显式配置 dead-letter 的后台 Hybrid outbox；默认路径不变。
- ChangedProviders：EasyCaching Redis/Hybrid；FreeRedis、CSRedis、Memory 不变。
- ChangedTFMs：生产 `netstandard2.0`；测试 `net6.0`、`net8.0`。
- ChangedBuildPackaging：无新增外部包。
- ChangedBenchmarkHarness：无。
- ChangedDocsOnly：README、迁移说明、索引和任务证据。
- AffectedDependents：显式启用 Hybrid outbox 的 DI 使用方和 outbox 集成测试。
- RiskLevel：HIGH；涉及失败消息状态转移，但默认关闭且容量不足时保留消息。

## 验收标准

- 默认配置与 093 行为一致，不创建 dead-letter 结构或改变无限重试。
- 达到阈值的消息只能在原子转移成功后从 processing 消失；dead-letter 满/取消/异常时仍在 processing。
- 进程重建后尝试次数继续累计，不能通过重启绕过阈值。
- 单元测试和真实 Redis 门禁在 net6/net8 通过，生产 netstandard2.0 构建通过。
- 更新索引时只关闭“Hybrid outbox 毒消息隔离”项；保留期、人工重放和跨进程指标仍保持未完成状态。

# BING-CACHE-TODO-20261007-096：Hybrid outbox dead-letter 保留期与有界清理

## 目标

在 095 的 dead-letter 隔离基础上补齐可选保留期和自动清理，避免死信集合无限累积，同时保持默认行为不删除死信。现有 `DeadLetterMaxMessages` 作为共享 Redis dead-letter 的消息数容量预算；本轮不假装提供精确 Redis 字节预算。

## 本轮契约

- `DeadLetterRetention=TimeSpan.Zero` 表示不自动过期，已有死信永久保留，保持 095 行为。
- 只有 dead-letter 已启用且 `DeadLetterRetention` 为正值时才运行自动清理；配置不完整或保留期超出上限时 fail fast。
- 清理以 dead-letter 转移时间为准，不使用原始入队时间；每次最多删除 `DeadLetterCleanupBatchSize` 条，避免长 Lua 脚本阻塞 Redis。
- 清理脚本按 score 原子选择并删除过期成员；并发转移与清理不会删除未过期消息，也不会影响 pending/processing/attempts。
- 清理失败只记录诊断并等待下一轮，不确认或修改 processing 消息；已发送的 Redis 命令不承诺撤销。
- 现有 `DeadLetterMaxMessages` 是 dead-letter 消息数上限，容量满时继续保留 processing；精确字节预算、人工查看/重放/删除和跨进程指标不在本轮。

## 实现范围

### P1：配置、状态和诊断

- 在 `HybridInvalidationDeliveryOptions` 增加 `DeadLetterRetention`、`DeadLetterCleanupInterval`、`DeadLetterCleanupBatchSize`，默认保留期为零，校验范围和死信启用关系。
- outbox 状态增加最早 dead-letter 时间；诊断快照增加最早 dead-letter 年龄和当前进程过期清理计数。
- outbox 接口增加按截止时间和批大小清理过期 dead-letter 的同步/异步方法。

### P2：Redis 原子清理和 worker 调度

- 使用同 hash tag 的 dead-letter Sorted Set，以转移时间作为 score。
- 增加 Lua 清理脚本：按 score 和批大小读取过期成员并在同一脚本内删除，返回实际删除数量。
- worker 按 `DeadLetterCleanupInterval` 有界调度清理；无保留期时不发送清理命令。
- 清理异常进入已有最近失败诊断，不中断正常 outbox drain。

### P3：测试、文档和索引

- 单元测试覆盖默认关闭、配置校验、清理调度、批大小和清理失败不影响 processing。
- 真实 Redis 测试覆盖过期 score 清理、未过期成员保留、批大小上限和清理后容量恢复。
- 更新 README、迁移说明、权威 TODO 索引、执行报告和生产符号映射。

## 不在本轮范围

- Redis 精确字节级空间预算和跨版本 dead-letter payload 大小重算。
- 人工查看、人工重放、人工确认删除及权限审计 API。
- 跨进程全局成功/失败/隔离/清理指标聚合。
- 写入提交到失效入队之间的事务 outbox 或补偿机制。
- 100K 性能矩阵、历史三方基线和外部双分片/独立总线门禁。

## Change Impact Analysis

- ChangedFiles：EasyCaching Hybrid outbox 配置、状态接口、Redis 实现、投递 worker、诊断快照；相关单元/Redis 集成测试、README、迁移说明、TODO 索引和任务证据。
- ChangedProjects：`Bing.EasyCaching`、`Bing.EasyCaching.Tests`；生产 netstandard2.0 构建作为依赖验证。
- ChangedPublicContracts：新增三个可选配置属性和两个诊断字段；缓存读写方法签名不变。
- ChangedRuntimePaths：仅影响显式启用 dead-letter 保留期的后台清理；默认路径和未设置保留期的 dead-letter 路径不变。
- ChangedProviders：EasyCaching Redis/Hybrid；FreeRedis、CSRedis、Memory 不变。
- ChangedTFMs：生产 `netstandard2.0`；测试 `net6.0`、`net8.0`。
- ChangedBuildPackaging：无新增外部包。
- ChangedBenchmarkHarness：无。
- ChangedDocsOnly：README、迁移说明、索引和任务证据。
- AffectedDependents：显式启用 Hybrid outbox 的 DI 使用方和 outbox 集成测试。
- RiskLevel：MEDIUM；清理是可选删除行为，受保留期和批大小限制，默认关闭。

## 验收标准

- 默认配置和 `DeadLetterRetention=0` 不发送清理命令，095 dead-letter 行为不变。
- 只有 score 不晚于截止时间的成员被删除，每次最多删除配置批大小；未过期消息和 processing 保持不变。
- 清理失败传播到诊断但不使 worker 丢弃或确认 processing 消息。
- 单元测试和真实 Redis 门禁在 net6/net8 通过，生产 netstandard2.0 构建通过。
- 更新索引时只关闭“dead-letter 保留期与有界清理”；精确字节预算、人工重放和跨进程指标仍保持未完成状态。

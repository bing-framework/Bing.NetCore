# BING-CACHE-TODO-20261007-097：Hybrid outbox dead-letter 精确 payload 字节预算

## 目标

补齐权威缓存 TODO 中“精确 dead-letter 字节级空间预算和历史 payload 大小重算”。在现有 `DeadLetterMaxMessages` 消息数上限之外，增加可选的 UTF-8 序列化 payload 字节上限，并保证 Redis outbox 的死信转移、清理、状态读取在并发进程下保持原子。

本轮只处理 Redis outbox 的逻辑 payload 字节预算。Redis allocator、Sorted Set/hash/list 元数据和碎片带来的实际进程内存不是稳定的业务预算，文档中明确区分，不把 payload 字节上限冒充物理内存上限。

## 当前实现与证据

- `HybridInvalidationDeliveryOptions` 目前只有 `DeadLetterMaxMessages`，没有字节上限。
- `RedisHybridInvalidationOutbox` 以 JSON envelope 作为 Sorted Set member，dead-letter Lua 只检查 `ZCARD`；清理 Lua 只 `ZREM`，未保存 member 大小。
- `HybridInvalidationOutboxStatus` 和 `HybridInvalidationDeliverySnapshot` 只报告死信数量/年龄，不报告 payload 字节数。
- 095/096 已验证消息数容量、原子死信转移和有界保留期清理；096 明确将精确字节预算和历史 payload 重算留为后续 TODO。

## 契约与边界

- 新增 `DeadLetterMaxBytes`，`0` 表示关闭字节限制并保持旧行为；正值表示所有 dead-letter JSON envelope UTF-8 payload 字节之和的上限。
- 正值字节预算必须与 `MaxDeliveryAttempts`、`DeadLetterMaxMessages` 一起启用；范围限制为 `1..1_073_741_824` 字节，避免配置溢出和误配超大预算。
- 字节检查与消息数检查都在 dead-letter Lua 中完成；任一预算不足时 processing 消息继续保留，`TryDeadLetter` 返回 false。
- Redis 以 `:sizes` hash 保存每个 dead-letter member 的 payload 字节数，以 `:bytes` string 保存总和。转移、清理和历史重算在 Lua 内原子完成。
- 启用字节预算且旧 dead-letter 缺少 `:sizes`/`:bytes` 记录时，脚本按 Sorted Set 当前 member 的实际 Redis bulk string 字节长度重建记录；该一次性 O(N) 重建只发生在计数缺失或 hash 数量不一致时。
- 清理删除 member 时同步删除 size 记录并递减总和；历史 member 没有 size 记录时使用实际 payload 字节长度补齐。
- 状态和诊断报告当前 dead-letter payload 字节数；未启用字节预算或没有可确定的记录时返回 `null`，不伪造零值。
- 默认配置、仅消息数配置、旧 envelope 读取、processing 恢复和 096 保留期语义保持兼容。

## 实施阶段

### P1：配置、状态和主调用链

- 在 `HybridInvalidationDeliveryOptions` 增加 `DeadLetterMaxBytes`，完成范围校验和 Clone 传播。
- 将字节上限传入 `RedisHybridInvalidationOutbox`；不改变注入 outbox 的生命周期管理。
- 扩展 outbox 状态及公开诊断快照，增加可空的 `DeadLetterBytes` 字段。

### P2：Redis 原子预算和历史重算

- 增加同命名空间的 sizes hash 和 bytes counter key。
- 重写 dead-letter Lua：在必要时原子重建历史 member 大小，先检查消息数/字节数，再转移 processing、写入 sorted set、写入 size hash 和递增总和。
- 更新清理 Lua：按 score 和批大小删除成员，同时删除大小记录、递减总和并在归零时删除 counter。
- 状态读取在字节预算启用时通过原子脚本确保并返回准确总和；同步/异步路径保持取消和异常传播。
- 保留默认 `DeadLetterMaxBytes=0` 的旧路径，不为未启用字节预算额外扫描死信集合。

### P3：测试、文档和索引

- 增加配置非法值、未启用兼容和快照字段单元测试。
- 增加真实 Redis 测试：精确边界允许/拒绝、拒绝时 processing 保留、清理递减总和、历史 member 重算、双 outbox 竞争下的原子预算。
- 更新 EasyCaching README、迁移说明、任务 TODO/execution/review/test-mapping 和权威索引。

## Change Impact Analysis

- ChangedFiles：EasyCaching outbox 配置、状态/诊断契约、Redis outbox、投递器构造调用；EasyCaching 单元/真实 Redis 测试；README、迁移说明和任务证据。
- ChangedProjects：`Bing.EasyCaching`、`Bing.EasyCaching.Tests`；生产 `netstandard2.0` 构建作为依赖验证。
- ChangedPublicContracts：新增一个可选配置属性和一个可空诊断字段；现有缓存读写签名不变。
- ChangedRuntimePaths：仅影响显式启用 `DeadLetterMaxBytes` 的 dead-letter 转移、清理和状态读取；默认路径保持旧行为。
- ChangedProviders：EasyCaching Redis/Hybrid；FreeRedis、CSRedis、Memory 不变。
- ChangedTFMs：生产 `netstandard2.0`；测试 `net6.0`、`net8.0`。
- ChangedBuildPackaging：无新增依赖。
- ChangedBenchmarkHarness：无。
- ChangedDocsOnly：README、迁移说明、TODO 索引和任务证据。
- AffectedDependents：显式启用 Hybrid Redis outbox 的 DI 使用方和 outbox 测试。
- RiskLevel：MEDIUM；新预算脚本可能在首次启用时对历史死信执行一次有界于消息数上限的 O(N) 重算，预算拒绝会让消息继续留在 processing，可能增加重试压力。

## 验证命令

先执行精准测试，再执行受影响项目完整测试和生产构建：

```powershell
dotnet test framework/tests/Bing.EasyCaching.Tests/Bing.EasyCaching.Tests.csproj --no-restore --framework net8.0 --filter FullyQualifiedName~HybridInvalidationDeliveryTest
dotnet test framework/tests/Bing.EasyCaching.Tests/Bing.EasyCaching.Tests.csproj --no-restore --framework net6.0 --filter FullyQualifiedName~HybridInvalidationDeliveryTest
dotnet test framework/tests/Bing.EasyCaching.Tests/Bing.EasyCaching.Tests.csproj --no-restore --framework net8.0 --filter FullyQualifiedName~RedisHybridInvalidationOutboxIntegrationTest
dotnet test framework/tests/Bing.EasyCaching.Tests/Bing.EasyCaching.Tests.csproj --no-restore --framework net6.0 --filter FullyQualifiedName~RedisHybridInvalidationOutboxIntegrationTest
dotnet test framework/tests/Bing.EasyCaching.Tests/Bing.EasyCaching.Tests.csproj --no-restore --framework net8.0
dotnet test framework/tests/Bing.EasyCaching.Tests/Bing.EasyCaching.Tests.csproj --no-restore --framework net6.0
dotnet build framework/src/Bing.EasyCaching/Bing.EasyCaching.csproj --no-restore -v:minimal
git diff --check
```

真实 Redis 测试继续要求显式设置 `RUN_INTEGRATION_TESTS=true`、`BING_TEST_ALLOW_REDIS_OUTBOX=true` 和非生产 `BING_TEST_HYBRID_OUTBOX_REDIS`；缺少环境时只记录外部阻塞。

## 验收标准

- 默认配置和 `DeadLetterMaxBytes=0` 的行为、命令路径和 095/096 测试保持通过。
- 正好等于预算的 payload 可以转移，超过预算的 payload 不转移且 processing/attempts 保留。
- 多进程同时尝试转移时，Redis 原子脚本不会使总字节数超过上限。
- 清理后 bytes counter 与剩余 member UTF-8 payload 字节总和一致，历史 member 可被自动重算。
- 状态/快照字段在启用预算时准确，在未启用时为 null。
- 双 TFM 精准/完整测试、真实 Redis 门禁和生产 `netstandard2.0` 构建通过；没有新增 `OPEN_ACTIONABLE` 代码缺口。

## 不在本轮范围

- Redis allocator 实际内存、跨进程全局指标后端、人工查看/重放/确认删除和权限审计。
- 写入提交到失效消息入队之间的事务 outbox 或补偿机制。
- 正式 100K 性能矩阵、严格三方 baseline 和外部双分片/独立总线门禁。

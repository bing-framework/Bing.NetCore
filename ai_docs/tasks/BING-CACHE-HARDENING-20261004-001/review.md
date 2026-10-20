<!-- AI_REVIEW_STATUS: PASS -->
AI_TASK_ID: BING-CACHE-HARDENING-20261004-001
AI_REVIEWED_AT: 2026-10-04T04:05:10Z

# 缓存模块独立复审

## 审查结论

FIX-001 至 FIX-004 均已由真实实现、直接测试和当前候选执行证据关闭。Redis Cluster 现在同时校验主节点身份和 0..16383 槽位完整覆盖；客户端分片用 `expectedShardCount` 与 Redis `run_id` 拒绝数量不足或实例重复。FreeRedis 运行中取消、Hybrid 总线发布失败及文档一致性也已闭环。当前 `OPEN_ACTIONABLE=0`，代码审查结论为 `PASS`。

性能对照证据可审计，但候选版在 10,000 键场景存在已记录的批量读、扫描和分配量回归，项目没有经批准的发布预算。该项保持 `BLOCKED_APPROVAL`，不转换成代码 Finding，也不触发自动性能修复。

## 状态分离

| 维度 | 状态 | 说明 |
| --- | --- | --- |
| IMPLEMENTATION_STATUS | PASS | `OPEN_ACTIONABLE=0`；FIX-001 至 FIX-004 均关闭 |
| TEST_STATUS | PASS_WITH_REUSED_EVIDENCE | Reviewer 精准测试通过；复用执行者对当前候选的双 TFM、隔离 Redis、Cluster 故障和跨进程证据 |
| PERFORMANCE_EVIDENCE_STATUS | PASS | 固定旧版提交、同构程序、原始 JSON 和 1k/10k 多轮结果均已落盘 |
| RESOURCE_EVIDENCE_STATUS | NOT_APPLICABLE | 当前计划没有独立容量矩阵门禁 |
| EXTERNAL_GATE_STATUS | PASS_BY_REUSED_EVIDENCE | Cluster 故障矩阵及三 Provider 跨进程矩阵已有当前候选证据 |
| PERFORMANCE_THRESHOLD_STATUS | BLOCKED_APPROVAL | 未批准吞吐、延迟、分配量或命令数回归预算 |
| RELEASE_STATUS | BLOCKED | 实现与测试通过；维护者仍需决定性能回归预算 |
| GOAL_STATUS | STOPPED_BLOCKED | 无可执行问题；仅剩 `BLOCKED_APPROVAL`，停止自动 Fix |

## Candidate Identity

- Git 基点：`24c93cb1a5355797223637e2d42ac3dce968d7ef`；候选为该提交上的未提交工作区。
- 本轮复审范围：新增的 `CacheClusterTopology`、两个原生 Provider 的扫描前后校验、`expectedShardCount`、FreeRedis 运行中取消、Hybrid 总线故障测试、迁移/横切文档及更新后的执行报告。
- 性能候选和旧版身份沿用 `performance.md`；本轮修复没有改变 FreeRedis/CSRedis 已采集的批量与扫描算法主体，因此不重复运行 L5。

## Change Impact Analysis

```text
ChangedFiles: 公共拓扑配置/校验、两个原生 Redis Provider、FreeRedis 协调、EasyCaching 测试、缓存文档
ChangedProjects: Bing.Caching、Bing.Caching.FreeRedis、Bing.Caching.CSRedis、Bing.EasyCaching.Tests
ChangedPublicContracts: CacheConfiguration 新增 expectedShardCount
ChangedRuntimePaths: Cluster 扫描前后校验、客户端分片构造、运行中取消
ChangedProviders: FreeRedis、CSRedis、EasyCaching Hybrid
ChangedTFMs: netstandard2.0 生产；net6.0/net8.0 验证
ChangedBuildPackaging: 否
ChangedBenchmarkHarness: 否
ChangedDocsOnly: 否
AffectedDependents: 多节点扫描/清理调用方和缓存迁移使用方
RiskLevel: HIGH
```

## 计划验收矩阵

| 计划项 | 结论 | 依据 |
| --- | --- | --- |
| P1 配置、键与生命周期 | PASS | 实例配置、版本化物理键、隐藏前缀拒绝和 DI 生命周期已接入 |
| P2 行为契约 | PASS | 覆盖写、毫秒 TTL、批量顺序、命中语义和运行中取消契约有直接证据 |
| P3 有界操作与安全清理 | PASS | 分批、有限并发、逐页扫描及命名空间 Clear 已实现 |
| P4 Cluster/分片 | PASS | Cluster 主节点身份与全槽覆盖、故障未切主、切主恢复、分片数量及重复实例均已验证 |
| P5 跨进程回源 | PASS | 三 Provider 的正常、退出、超时、失租和等待超时矩阵通过 |
| P6 EasyCaching | PASS | Redis/Hybrid 原子提交、跨实例失效及同步/异步总线发布失败均走真实主调用链 |
| P7 Core 集合缓存 | PASS | 锁内检查修改、浅快照、返回值及并发测试通过 |
| P8 验证与文档 | PASS | 构建/测试门禁、迁移文档、包 README 和横切能力总览一致 |
| P9 性能 | DEVIATED_OK | 对照证据完整且诚实记录回归；发布预算需维护者批准 |

## 已关闭问题

### FIX-001

- 严重程度：HIGH。
- 处理要求：MUST_FIX。
- 状态：CLOSED。
- 证据：`CacheClusterTopology.cs:12-45` 解析在线主节点的单槽/槽位范围，拒绝非法范围、重复归属和 0..16383 缺口；`CacheClusterTopologyTest.Validate_RequiresEveryActiveMasterExactlyOnce` 新增故障主节点尚未提升的 `noFailover` 反例。两个 Provider 仍在扫描前后调用该校验；真实切主后的 FreeRedis 3/3、CSRedis 2/2 net8 故障矩阵继续通过。客户端分片另由 `CacheShardTopology` 的 `run_id` 唯一性校验及两个 Provider 的 `RepeatedShard_IsRejectedEvenWhenExpectedCountMatches` 覆盖。

### FIX-002

- 状态：CLOSED。
- 证据：`FreeRedisCacheManager.Coordination.cs:203-214` 在超时分支及写回前检查调用方令牌；`TtlCancellationRecoveryTest.CallerCancellation_PropagatesOperationCanceled_WhenRetrieverIgnoresToken_AndRecovers` 覆盖工厂已启动且忽略令牌后的取消、无写回和后续恢复。FreeRedis net6/net8 当前候选各 74 项通过。

### FIX-003

- 状态：CLOSED。
- 证据：`RedisCoordinatorIntegrationTest.cs:25-39,170-203` 用可控失败的 `IEasyCachingBus` 构造真实 `HybridCacheManager`，同步和异步路径均断言异常传播、Redis 已提交、本地已清除、其他实例仍可能持旧值以及后续读取恢复。EasyCaching net6/net8 当前候选各 22 项通过。

### FIX-004

- 状态：CLOSED。
- 证据：`docs/migrations/cache-namespace-v2.md:11`、`framework/src/Bing.EasyCaching/README.md:40-44` 与 `docs/guides/横切能力文档.md:224-230` 现均说明 Redis/Hybrid 已接入跨进程租约与原子提交，并记录 Hybrid 发布失败显式传播、Redis 已提交值不可回滚及回源需幂等的边界。全文检索不再命中旧的“尚无/尚未接入”表述。

## 性能与发布门禁

- 旧版基点、源 Blob、运行时、端点、样本数和原始 JSON 均可复核。
- 10,000 键下，FreeRedis 批量读约增加 44.7%，扫描约增加 20.8%，分配量约增加 81.1%；CSRedis 批量读约增加 49.5%，扫描约增加 39.3%，分配量约增加 107.9%，服务器命令统计约增加 49.9%。
- 正常同键 16 路回源从每轮 16 次降为 1 次，并新增命名空间、租约和失租拒写语义；这些成本不能表述为全面性能提升。
- 当前没有批准的回归预算，故 `PERFORMANCE_THRESHOLD_STATUS=BLOCKED_APPROVAL`。只有维护者批准成本或给出阈值后，才能形成发布判断；该状态不允许自动进入性能 Fix。

## 验证证据

| 等级 | 结果 |
| --- | --- |
| L0 | Reviewer 执行 `git diff --check`，通过 |
| L1 | Reviewer 在最终 Cluster 修复落盘后执行公共拓扑/配置精准测试 net8：4/4 通过；两个 Provider 的重复分片测试由执行者报告通过 |
| Reused L2 | `Bing.Caching.Tests` net6/net8 各 105；FreeRedis 各 75；CSRedis 各 72；EasyCaching 各 22 |
| Reused L3 | Cluster 故障矩阵 FreeRedis 3/3、CSRedis 2/2，net6/net8 均通过；三 Provider 跨进程故障矩阵通过 |
| Build | `Bing.All.sln --no-restore` 当前候选构建 0 错误；已有依赖/目标框架警告不属于本任务新增问题 |
| L5 | 复用 `performance.md` 与 `bench/*-v2.json`；本轮未改变对应基准范围 |

Reviewer 没有重复启动隔离 Docker 环境；依据 GOAL_RULES 复用执行者在同一候选、相同 TFM 和环境类别下的新鲜证据。最终 Cluster 槽位修复由 Reviewer 通过源码、直接单测和既有真实切主矩阵共同验收。

## Goal Summary

```text
Task: BING-CACHE-HARDENING-20261004-001
Round: Independent Review 2

Implementation: PASS
Tests: PASS_WITH_REUSED_EVIDENCE
Open Actionable: none
Open Actionable Count: 0
Closed: FIX-001, FIX-002, FIX-003, FIX-004
Blocked Approval: performance regression budget
Blocked External: none
Not Applicable: independent resource/capacity matrix
Accepted Limitations: cold-cache migration; non-atomic namespace Clear; failover not strict once-only; Hybrid final consistency
Verified Boundaries: listed offline Cluster node fails; promoted topology restores full operations; expired/lost owner cannot write back
Deferred: none
No-Progress Check: CHANGED
Next Action: 停止自动 Fix；性能预算等待维护者决策
Goal Status: STOPPED_BLOCKED
```

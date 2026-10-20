<!-- AI_REVIEW_STATUS: NEEDS_FIX -->
AI_TASK_ID: BING-CACHE-TODO-20261004-003
AI_REVIEWED_AT: 2026-10-04T12:00:21.6457688Z

# 独立审查

## 状态

- IMPLEMENTATION_STATUS: NEEDS_FIX
- FUNCTIONAL_STATUS: PASS
- TEST_STATUS: PASS_WITH_DEFERRED_GAPS
- PERFORMANCE_EVIDENCE_STATUS: PARTIAL_BUDGET_FAILED
- EXTERNAL_GATE_STATUS: PASS
- RELEASE_STATUS: BLOCKED
- GOAL_STATUS: STOPPED_NO_PROGRESS

本轮审查对照 `plan.md`、`execution.md`、当前源码、测试和 Git Diff，重点检查 EasyCaching 客户端分片节点身份复验、Hybrid 总线失败传播、DI 注册与批量写删调用链。审查未修改生产代码或测试。

## Change Impact Analysis

- ChangedProjects: `Bing.EasyCaching`、`Bing.EasyCaching.Tests`。
- ChangedPublicContracts: 新增客户端分片 Redis/Hybrid 注册和 `ShardedRedisCacheManager`；`HybridCacheManager` 类型保留公开，但构造器收窄为程序集内部，外部统一通过 DI 注册获取实例。
- ChangedRuntimePaths: Hybrid 单键/批量写删、总线发布、客户端分片单键/批量读写和扫描。
- ChangedProviders: EasyCaching Redis、Hybrid，覆盖单节点、Redis Cluster 和客户端分片。
- ChangedTFMs: 生产 `netstandard2.0`；测试 `net6.0`、`net8.0`。
- RiskLevel: HIGH，原因是 DI 生命周期、总线异常传播及分片拓扑完整性均影响跨实例一致性。

## Reviewer 验证

- `dotnet build framework/src/Bing.EasyCaching/Bing.EasyCaching.csproj --no-restore --framework netstandard2.0`：通过，0 警告、0 错误。
- EasyCaching 精准测试：`HybridBatchBusFailure_*`、`HybridManager_RejectsUntrackedBus` 与 `ShardedIdentityUnitTest` 共 10/10 通过（net6.0、net8.0）。
- 隔离双 Redis 节点完整回归：复用执行者当前源码上的 net6.0、net8.0 各 51 通过、2 个 Cluster 环境门控跳过。
- `git diff --check`：通过。
- 复用 `execution.md` 中当前源码范围未失效的三主三从 Cluster、FreeRedis/CSRedis 命令级取消和固定性能基线证据。

## 验收矩阵

| 能力 | 结论 | 审查结果 |
| --- | --- | --- |
| Hybrid 总线失败追踪 | PASS | `HybridBusFailureTracker` 记录 EasyCaching Hybrid Provider 内部吞掉的发布异常，适配层在 Provider 返回后补抛；成功重试会清除前次失败。 |
| 单节点 Hybrid DI | PASS | 原始 Redis 总线由跟踪器包装，Hybrid Provider 与 `HybridCacheManager` 使用同一跟踪器；批量发布失败精准测试通过。 |
| 分片 Hybrid DI | PASS | 每个分片具有独立 Provider、总线跟踪器和主题，按总线名称解析，不会把失效处理器覆盖到其他分片。 |
| Hybrid 批量失败后的层状态 | PASS | 真实 `HybridCachingProvider` 配合可控本地/分布式 Provider，证明 `SetAllAsync`、`RemoveAllAsync` 完成层操作后仍传播总线异常。 |
| 分片批量读取期间节点替换 | PASS | 同步与异步批量读取在命令前后复验全部分片 `run_id`；精准测试覆盖读取期间身份变化。 |
| 分片单键操作期间节点替换 | PASS | 四个同步/异步 helper 均在成功返回前复验目标分片；Get、GetAsync、Set、RemoveAsync 在途替换测试覆盖四种 helper。 |
| `HybridCacheManager` 构造路径 | PASS | 构造器及 tracker 均收窄为程序集内部；公开支持入口统一为 DI 注册，由注册代码把同一 tracker 交给 Provider 和管理器。 |
| EasyCaching 双分片功能回归 | PASS | Redis/Hybrid 路由、批量、流式、扫描、清理、回源与失效测试在 net6/net8 均通过。 |
| 性能预算 | FAIL | 扫描 P95、CSRedis 批量读 P95 及两个 Provider 分配量仍超预算，见 FIX-007。 |

## Findings

### FIX-007

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 状态：OPEN_ACTIONABLE / STOPPED_NO_PROGRESS_FOR_SAME_EXPERIMENT
- 问题：既定性能预算仍未达到。FreeRedis 当前 1k 候选扫描 P95 回退约 34.1%、批量读平均分配回退约 203.7%；CSRedis 批量读/扫描 P95 回退约 41.5%/36.1%，批量读平均分配回退约 431%。
- 证据：`performance.md`、当前候选 1k 原始 JSON 和 `execution.md` 的性能结论；本轮 EasyCaching 正确性改动没有改变该性能候选。
- 影响：缓存批量路径性能优化的明确发布预算未闭环。
- 修复目标：在保持命名空间隔离、顺序、取消和拓扑语义的前提下，使批量读/扫描 P95 回退不超过 20%，平均分配回退不超过 30%。
- 修复要求：先取得新的结构性根因证据；不得重复已经两次无实质收益的 SHA 实例或返回类型微调，不得自行放宽用户批准的预算。
- 验证方式：候选实现发生实质变化后，使用固定 baseline、同一探针和相同环境生成 1k/10k current JSON，重算延迟和分配，并运行对应 Provider 契约、分片及 netstandard2.0 构建。

### FIX-013

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 状态：CLOSED
- 解决结果：全部单键入口集中到四个 `ExecuteForKey` 同步/异步 helper；每个 helper 固定目标分片，并只在命令成功后执行第二次 `run_id` 校验，因此拓扑变化不会作为成功返回，命令自身异常也不会被后置校验覆盖。
- 证据：`Get_FailsWhenShardRunIdChangesDuringRead`、`GetAsync_FailsWhenShardRunIdChangesDuringRead`、`Set_FailsWhenShardRunIdChangesDuringWrite`、`RemoveAsync_FailsWhenShardRunIdChangesDuringDelete` 分别覆盖同步返回值、异步返回值、同步无返回值和异步无返回值四种 helper；net6.0、net8.0 精准测试各 10/10 通过。

### FIX-014

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 状态：CLOSED
- 解决结果：`HybridCacheManager` 构造器和 `HybridBusFailureTracker` 均收窄为 `internal`，外部程序集不能手工组合可能错配的 Provider 与 tracker。公开支持路径统一为 `AddHybridCaching` 和 `AddShardedHybridCaching`，注册代码创建同一个 tracker 并同时传给 Hybrid Provider 与管理器。
- 证据：生产符号可见性已收窄；README 只承诺 DI 统一装配，不再指导手工构造；`HybridManager_RejectsUntrackedBus` 保留程序集内部不变量测试，两个 Hybrid 批量发布失败测试和双 Redis 完整回归继续通过。

## 其他状态分类

- CLOSED：EasyCaching `CacheKey` 流式输入惰性消费、批次取消、10k List/Stream 一致性与特殊键流式扫描。
- CLOSED：EasyCaching Redis Cluster 与客户端分片的跨槽/跨节点批量、扫描、清理和 Hybrid 跨实例失效基本能力。
- CLOSED：FreeRedis/CSRedis 命令级取消证据、Cluster 正常/缺失主节点/副本晋升测试。
- CLOSED：Hybrid 单节点和分片 DI 路径的批量总线失败报告。
- CLOSED：FIX-013，四种单键 helper 均完成命令前后身份复验并有直接测试。
- CLOSED：FIX-014，Hybrid 手工装配从公共契约移除，DI 注册统一控制 tracker 身份。
- DEFERRED：真实节点重启期间的连接中断/恢复、总线运行期断线恢复、复杂重定向和拓扑反复变化专项环境测试。
- DEFERRED：100k、双分片 10k/100k 及 EasyCaching 多拓扑性能矩阵；FIX-007 没有结构性候选前不重复高成本矩阵。
- ACCEPTED_LIMITATION：当前客户端没有可靠公开钩子暴露网络请求数和 Provider 内部实际并发；报告保持不可观测，不用 Redis 命令数替代。
- NOT_APPLICABLE：历史版本没有等价 Cluster 流式能力，Cluster 当前指标不计算历史回退比例。
- BLOCKED_EXTERNAL：无；适用的单节点、双分片和三主三从环境已有证据。
- BLOCKED_APPROVAL：无；性能阈值已经由用户批准。

## Goal Summary

- Implementation: EasyCaching 功能实现通过；FIX-013 与 FIX-014 已关闭，当前仅剩独立性能门禁 FIX-007。
- Tests: Reviewer 精准复验 net6/net8 各 10/10 通过；执行者完整回归各 51/51 通过、2 项 Cluster 环境门控跳过；生产 netstandard2.0 构建通过。
- Performance: FIX-007 预算失败，且同类微调受 No Progress Guard 约束。
- Open Actionable: FIX-007。
- Next Action: 按 No Progress Guard 停止同类实验；FIX-007 仅在取得新的结构性根因后继续。
- Goal Status: STOPPED_NO_PROGRESS。

## 结论

FIX-013 与 FIX-014 已由实现、直接测试和回归证据关闭，EasyCaching 功能审查通过。既定性能预算 FIX-007 仍是未解决的 MUST_FIX，且当前没有新的结构性根因，审查状态维持 `NEEDS_FIX`，Goal 按 No Progress Guard 停止重复实验。

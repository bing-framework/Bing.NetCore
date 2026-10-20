<!-- AI_REVIEW_STATUS: NEEDS_FIX -->
AI_TASK_ID: BING-CACHE-PERF-STREAM-20261004-002
AI_REVIEWED_AT: 2026-10-04T08:30:17.0425508Z

# 独立审查

## 状态

- IMPLEMENTATION_STATUS: NEEDS_FIX
- FUNCTIONAL_STATUS: PASS_WITH_EXTERNAL_GAP
- TEST_STATUS: PASS_WITH_CLUSTER_EXTERNAL_GAP
- PERFORMANCE_EVIDENCE_STATUS: PARTIAL_BUDGET_FAILED
- RESOURCE_EVIDENCE_STATUS: PARTIAL
- EXTERNAL_GATE_STATUS: BLOCKED_EXTERNAL
- RELEASE_STATUS: BLOCKED
- GOAL_STATUS: STOPPED_NO_PROGRESS（FIX-007 仍为 OPEN_ACTIONABLE，不再自动重复同类实验）

本审查对照 `plan.md`、`execution.md`、`performance.md`、当前生产代码、测试代码、Git Diff 和原始 benchmark JSON 完成。审查只更新本文件，没有修改业务代码或测试代码。

## Change Impact Analysis

- ChangedPublicContracts: `CacheEntry<T>`、`ICache.GetAllStreamAsync<T>`、`ICache.GetByPrefixStreamAsync<T>`；CSRedis 构造器新增可选 `shardNodeIndex`。
- ChangedRuntimePaths: FreeRedis、CSRedis、EasyCaching Redis/Hybrid 的批量读取、前缀扫描、取消、分片/Cluster 路由和 Hybrid 本地回填；CSRedis 扫描流保留已验证的物理键/逻辑键成对批缓冲。
- ChangedBenchmarkHarness: baseline/current、预热/采样、流式阶段、多拓扑与 EasyCaching Provider 构造、Redis 命令监控和资源指标。
- ChangedTFMs: 生产 netstandard2.0；测试 net6.0/net8.0；benchmark net8.0。
- RiskLevel: HIGH。

## 验证证据

- 公共契约测试：`Bing.Caching.Tests` net6.0/net8.0 各 105/105 通过。
- Provider 集成测试：FreeRedis net6.0/net8.0 各 79 通过、5 跳过；CSRedis net6.0/net8.0 各 77 通过、4 跳过。跳过项为未提供的 Cluster/专项性能门控。
- EasyCaching：net6.0/net8.0 各 25/25 通过，覆盖 Redis/Hybrid、租约和总线相关路径。
- CSRedis 自定义 `NodeRule` 精准测试 `CustomNodeRule_UsesMatchingShardMappingForBatchRead` 在双独立节点通过；相关 netstandard2.0 构建通过。
- baseline/current benchmark 工程均构建成功；固定 baseline 为 `24c93cb1a5355797223637e2d42ac3dce968d7ef`。
- 12 份 `formal-*.json` 中，既有 10 份单节点/双分片样本以及新增 EasyCaching Redis/Hybrid 样本均可审计。两个 EasyCaching 文件各含 1,000 项、2 轮预热、10 个 `rawSamples` 和 `write/hit/batchRead/batchReadStream/scan/scanStream/stampede/remove` 八阶段。
- EasyCaching JSON 重算值与 `performance.md` 一致：Redis 批量读/扫描 P95 6.4948/8.4776 ms，平均分配 1,585,042.4/2,057,695.2 bytes；Hybrid 为 5.8236/7.169 ms 和 1,874,139.2/2,353,981.6 bytes。
- CSRedis 直连分片修正后重新生成正式 1k 样本：批量读/扫描 P95 为 9.3373/19.329 ms，2 轮预热、10 个原始样本，与报告一致。
- 最新 CSRedis 扫描流修改经 netstandard2.0 构建和 12 个精准测试验证；`diagnostic-current-csredis-no-rehash-1k.json` 含 2 轮预热、10 个原始样本，扫描平均分配 5,860,625.6 bytes、P95 15.1031 ms。它证明平均分配从局部改动前 7.07 MB 降至 5.86 MB，但相对固定基线 1.24 MB 仍约回退 373%。
- 第二次且最后一次同类诊断 `diagnostic-current-csredis-string-mget-1k.json` 在相同 1k、2 轮预热、10 个原始样本条件下，将 MGET 原始类型改为 `string`；批量/扫描平均分配为 5.18/5.88 MB，P95 为 7.62/15.87 ms，未优于保留候选。该生产改动已撤回，当前代码继续使用 `byte[]`。
- `git diff --check` 通过；最新 FreeRedis 非 Cluster MGET 返回数量检查后的精准测试 12/12 通过，netstandard2.0 构建成功。

## 验收矩阵

| 计划项 | 结论 | 审查结果 |
| --- | --- | --- |
| 公共异步流契约及 netstandard2.0 | PASS | `CacheEntry<T>` 与两个异步流成员已接入全部实现，兼容依赖存在。 |
| List API 顺序、重复键、缺失、null/default | PASS | List 从共享流式路径收集；公共契约及 Provider 测试通过。 |
| 有界输入消费与取消 | PASS | 字符串和 `CacheKey` 输入均惰性投影、按批消费；派发新命令和产出前检查取消。 |
| 命名空间与字面量前缀 | PASS | 扫描限定实例命名空间并二次过滤；空格等非空前缀按字面量处理。 |
| FreeRedis 单节点/双分片批量路径 | PASS | MGET、原索引回填、返回数量校验和双节点实机测试闭环。 |
| CSRedis 单节点/双分片及自定义路由 | PASS | 显式物理键到节点索引映射、索引校验和目标扫描客户端直连已实现；默认/自定义路由双节点测试通过。 |
| Cluster 槽分组及有界并发 | PARTIAL | 源码实现 CRC16 槽分组、`MaxConcurrency` 有界派发和返回数量校验；无三主三从运行证据。 |
| EasyCaching Redis/Hybrid 功能 | PASS | 批量读取、本地回填、流式输出、租约与失效通知测试通过。 |
| benchmark 多 Provider、多拓扑及流式阶段 | PASS_WITH_EXTERNAL_GAP | FreeRedis、CSRedis、EasyCaching Redis/Hybrid 已有真实样本；单节点与双分片可构造，stream 阶段不完整物化。Cluster 环境缺失。 |
| 性能预算 | FAIL | 局部优化前的完整 1k/10k 矩阵显示 FreeRedis/CSRedis 批量读、扫描 P95 及分配量全部超预算；最新 CSRedis 1k 诊断虽降低扫描分配，仍约回退 373%，见 FIX-007。 |
| EasyCaching 性能证据 | PASS_WITH_NA_BASELINE | Redis/Hybrid 当前版各有正式 1k 八阶段样本；固定历史提交无等价项目，before 正确标为 `NOT_APPLICABLE`。 |
| 报告准确性 | PASS | `execution.md`、`performance.md` 与最新测试、分片重测、EasyCaching JSON 及证据边界一致。 |

## Findings

### FIX-007

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 状态：OPEN_ACTIONABLE
- 问题：用户批准的任务验收预算未达到。局部扫描优化前的完整矩阵中，FreeRedis 1k/10k 批量读 P95 回退为 +34.4%/+68.7%，扫描为 +23.2%/+59.9%；CSRedis 为 +37.3%/+45.5% 和 +27.4%/+32.2%。平均分配回退为 +184.2% 至 +510.9%，P95 分配也全部超过 30%。保留的 no-rehash 候选将 CSRedis 1k 扫描平均分配从 7.07 MB 降至 5.86 MB，但相对基线仍约 +373%，扫描 P95 为 15.1031 ms。第二个 string MGET 候选也无收益并已撤回。
- 证据：`performance.md`、8 份单节点 `formal-baseline/current-*.json`、`diagnostic-current-csredis-no-rehash-1k.json` 和 `diagnostic-current-csredis-string-mget-1k.json`；每组均为 2 轮预热、10 轮正式样本，原始样本与汇总可重算一致。正式表格是最后一次局部扫描优化前的完整 1k/10k 矩阵，不作为最新候选全部指标冒充精确证据。
- 影响：本阶段最核心的批量路径性能目标未完成，不能将实现标记为性能通过或批准发布。
- 修复目标：定位 `CacheKeyFormat`、批量反序列化、流收集和扫描回读的分配与耗时来源，在保持命名空间、租约、顺序和取消语义下达到既定预算。
- 修复要求：不得通过删除隔离/安全语义或放宽阈值闭环；修改热路径后使用同一固定 baseline、探针、Redis 环境和采样参数复测 1k/10k。
- 验证方式：保存新的 current 原始 JSON，重算 P95 与平均/P95 分配，并运行对应 Provider 契约、分片和 netstandard2.0 验证。
- 自动循环：同一 FIX-007 已完成一次有效诊断和一次正式确认实验；第二个候选没有新根因或收益并已撤回，触发 No Progress Guard。只有出现新的根因证据或实质不同的优化方案后才恢复，不再仅重复 benchmark。

## Closed Findings

- FIX-008 CLOSED：benchmark 已支持 `easyredis`/`easyhybrid` 单节点真实 Provider 构造；两个正式 JSON 各含 2 轮预热、10 轮样本、八阶段和流式绝对指标。baseline 对缺少等价项目显式 fail fast 并标为 `NOT_APPLICABLE`，报告未伪造回退比例。
- FIX-009 CLOSED：CSRedis 构造器接受作用于物理键的可选 `shardNodeIndex`；分片读取按该映射分组、拒绝越界索引并直连 `_scanClients[group.Key]`。README 明确自定义 `NodeRule` 必须提供同序映射，双节点自定义路由测试及全量 net6.0/net8.0 测试通过。
- CLOSED：CSRedis 前缀扫描流以物理键/逻辑键成对批缓冲，将已通过命名空间校验的物理键直接交给 MGET，不再由逻辑键重复生成物理键；netstandard2.0 构建、12 个精准测试和 1k 诊断样本闭环。
- CLOSED：FreeRedis/CSRedis 的字符串与 `CacheKey` 流输入不再全量 `ToArray`，按 `BatchSize` 惰性消费。
- CLOSED：Cluster 流式读取按 CRC16 槽分组，并以 `MaxConcurrency` 有界派发 MGET。
- CLOSED：空白但非空的前缀按字面量处理。
- CLOSED：benchmark 的 stream 阶段不完整物化，状态为 `MEASURED_WHEN_RUN`；多拓扑构造和节点监控已接入。
- CLOSED：FreeRedis 非 Cluster MGET 增加返回数量校验；相关精准测试和 netstandard2.0 构建通过。
- CLOSED：`execution.md`、`performance.md` 和 benchmark README 已同步最新实现与证据。

## 其他状态分类

- BLOCKED_EXTERNAL：缺少三主三从隔离 Redis Cluster，Cluster 功能、故障转移、重定向及性能矩阵尚不能运行；不得用单节点或双分片结果替代。
- DEFERRED：100,000 项完整矩阵未运行。1k/10k 已明确证明核心 Provider 预算失败，当前继续高成本采样不会改变验收结论；应在 FIX-007 优化后或资源检查点执行。
- NOT_APPLICABLE：历史版本没有流式 API、等价分片构造或 EasyCaching benchmark 项目，相关 before 不应伪造；上一阶段中间版缺少同构可重建候选，现有 v2 JSON 不可用于严格三方比例。
- ACCEPTED_LIMITATION：客户端没有可靠公开接口暴露网络请求数和 Provider 内部在途并发，报告用 `-1` 及明确状态标注；Redis 命令数没有冒充网络请求数。
- ACCEPTED_LIMITATION：兼容 List API 必须物化最终列表，其内存复杂度随结果数量增长；流式 API 已提供替代路径。
- BLOCKED_APPROVAL：无。性能阈值已由用户计划明确设为本阶段验收标准，Reviewer 不自行放宽；生产发布批准仍不在本任务授权范围。

## Goal Summary

Task: `BING-CACHE-PERF-STREAM-20261004-002`

- Implementation: 主要功能完成，1 个可执行性能问题未关闭。
- Release Gates: 功能与适用测试通过；性能预算失败；Cluster 外部门禁未运行。
- Validation Run: L0 构建/`git diff --check`，L1 保留的 no-rehash 扫描路径通过 12 个精准测试及 netstandard2.0 构建，L2 复用 Provider/EasyCaching 完整 net6.0/net8.0，L5 复用正式矩阵并核对两个 CSRedis 1k 诊断；未重复运行 heavy matrix。
- Open Actionable: FIX-007。
- Blocked Approval: none。
- Blocked External: 三主三从 Cluster。
- Not Applicable: 历史 stream/多拓扑/EasyCaching before、不可重建的严格中间版三方对照。
- Accepted Limitations: 不可观测网络请求/内部并发、兼容 List 物化。
- Deferred: 100k 完整矩阵。
- No-Progress Check: UNCHANGED（第二个同类候选无收益且已撤回）。
- Next Action: STOP；等待新的根因证据或实质不同的优化方案，不再重复同类 benchmark。
- Goal Status: STOPPED_NO_PROGRESS。

## 结论

FIX-008 与 FIX-009 已闭环。保留的 CSRedis no-rehash 优化取得可测的分配下降，但仍远超 30% 分配预算；第二个 string MGET 实验无收益并已撤回。FIX-007 仍是未解决的 MUST_FIX，因此 Review 状态维持 `NEEDS_FIX`；同类实验已达到两次上限，Goal 以 `STOPPED_NO_PROGRESS` 终止，不再要求新 benchmark。

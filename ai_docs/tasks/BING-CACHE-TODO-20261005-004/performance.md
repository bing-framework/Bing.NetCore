# 缓存性能正式预算报告

> 本报告引用的 JSON 产物为本地归档，位于 `artifacts/ai-tasks/`，不随普通 Git clone 分发。

<!-- AI_EXECUTION_STATUS: PARTIAL -->
<!-- AI_TASK_ID: BING-CACHE-TODO-20261005-004 -->
<!-- AI_PERFORMANCE_STATUS: COMPLETE_WITH_BOUNDARIES_BUDGET_FAILED -->
<!-- AI_REPORT_DATE: 2026-10-05 -->

本报告只使用已经落盘的正式预算 JSON、最终矩阵状态和 `summary.csv`，不重跑 benchmark。task004 的 14 个正式矩阵 cell 已结束，但其中 7 个以 `VERIFIED_TIMEOUT_BOUNDARY` 结束，没有完整 JSON 统计；报告不把超时解释为能力不支持。

## 结论

当前候选不能通过本阶段性能预算：

- 命中读请求 P95：4/4 通过，预算为回退不超过 10%。
- 批量读 P95：1/4 通过，预算为回退不超过 20%。
- 前缀扫描 P95：2/4 通过，预算为回退不超过 20%。
- 批量读平均分配和扫描平均分配：0/8 通过，预算为回退不超过 30%。

因此采集结束标签为 `COMPLETE_WITH_BOUNDARIES_BUDGET_FAILED`，执行标记仍为 `PARTIAL`。预算结论为 `FAIL`，观测缺口保持 `DEFERRED`，严格三方源码对照保持 `BLOCKED_EXTERNAL`。上表预算只覆盖单节点 CSRedis/FreeRedis 的 1,000 和 10,000 项；下文补充 5 个完整 Easy 100,000 项 cell 的绝对观测，不将其解释为跨 Provider 提升或预算通过。

## 正式预算口径

当前值来自 [budget-csredis.json](../../../artifacts/ai-tasks/BING-CACHE-TODO-20261005-004/performance-matrix/budget-csredis.json) 和 [budget-freeredis.json](../../../artifacts/ai-tasks/BING-CACHE-TODO-20261005-004/performance-matrix/budget-freeredis.json)；基线来自 task003 冻结的 [CSRedis baseline](../../../artifacts/ai-tasks/BING-CACHE-TODO-20261004-003/keyparse-formal-baseline-csredis-1k10k.json) 和 [FreeRedis baseline](../../../artifacts/ai-tasks/BING-CACHE-TODO-20261004-003/keyparse-formal-baseline-freeredis-1k10k.json)。四份文件的环境均为 .NET 8.0.30、Redis `127.0.0.1:52800`、单节点、1,000/10,000 项、2 轮预热、10 轮正式样本、命中读取 100 次和请求并发配置 16。

每个百分比按以下公式由原始字段直接计算，未放宽预算：

```text
delta = (current / baseline - 1) * 100
```

表中的耗时单位为毫秒，分配单位为字节；`PASS` 表示 delta 小于或等于该指标预算。

| Provider | 规模 | 批读 P95 | 扫描 P95 | 批读平均分配 | 扫描平均分配 | 命中读请求 P95 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| CSRedis | 1,000 | 5.2866 -> 6.0596，+14.6% PASS | 11.0419 -> 12.9601，+17.4% PASS | 926,326.4 -> 2,245,920，+142.5% FAIL | 1,250,480.8 -> 2,488,498.4，+99.0% FAIL | 0.5356 -> 0.4678，-12.7% PASS |
| CSRedis | 10,000 | 41.1861 -> 55.4314，+34.6% FAIL | 95.8980 -> 118.3812，+23.4% FAIL | 8,423,107.2 -> 21,904,104，+160.0% FAIL | 11,745,724.8 -> 24,334,536，+107.2% FAIL | 0.4631 -> 0.4849，+4.7% PASS |
| FreeRedis | 1,000 | 4.9588 -> 6.0789，+22.6% FAIL | 10.3349 -> 12.5483，+21.4% FAIL | 1,871,789.6 -> 2,955,476，+57.9% FAIL | 2,853,064 -> 4,648,718.4，+62.9% FAIL | 0.4799 -> 0.4919，+2.5% PASS |
| FreeRedis | 10,000 | 43.5465 -> 57.5343，+32.1% FAIL | 197.8003 -> 131.5175，-33.5% PASS | 17,989,596.8 -> 29,279,483.2，+62.8% FAIL | 27,879,869.6 -> 46,123,190.4，+65.4% FAIL | 0.5012 -> 0.4534，-9.5% PASS |

这里的命中读指标是 `results[].hitRequestLatencyMs.p95`，没有使用包含 100 次请求总耗时的 `phases.hit.elapsedMs.p95`。

## 矩阵状态

请以 [run-status.json](../../../artifacts/ai-tasks/BING-CACHE-TODO-20261005-004/performance-matrix/run-status.json) 为本轮矩阵运行状态的唯一入口。14 个 cell 已全部结束：7 个 `COMPLETED`、7 个 `VERIFIED_TIMEOUT_BOUNDARY`、0 个 `FAILED`。

| Cell | Provider | Topology | Scale | Terminal status | Exit |
| --- | --- | --- | --- | --- | ---: |
| `budget-freeredis` | FreeRedis | single | 1,000/10,000 | `COMPLETED` | 0 |
| `budget-csredis` | CSRedis | single | 1,000/10,000 | `COMPLETED` | 0 |
| `matrix-single-freeredis` | FreeRedis | single | 100,000 | `VERIFIED_TIMEOUT_BOUNDARY` | -1 |
| `matrix-single-csredis` | CSRedis | single | 100,000 | `VERIFIED_TIMEOUT_BOUNDARY` | -1 |
| `matrix-single-easyredis` | EasyRedis | single | 1,000/10,000/100,000 | `COMPLETED` | 0 |
| `matrix-single-easyhybrid` | EasyHybrid | single | 1,000/10,000/100,000 | `COMPLETED` | 0 |
| `matrix-sharded-freeredis` | FreeRedis | sharded | 1,000/10,000/100,000 | `VERIFIED_TIMEOUT_BOUNDARY` | -1 |
| `matrix-sharded-csredis` | CSRedis | sharded | 1,000/10,000/100,000 | `VERIFIED_TIMEOUT_BOUNDARY` | -1 |
| `matrix-sharded-easyredis` | EasyRedis | sharded | 1,000/10,000/100,000 | `COMPLETED` | 0 |
| `matrix-sharded-easyhybrid` | EasyHybrid | sharded | 1,000/10,000/100,000 | `COMPLETED` | 0 |
| `matrix-cluster-freeredis` | FreeRedis | cluster | 1,000/10,000/100,000 | `VERIFIED_TIMEOUT_BOUNDARY` | -1 |
| `matrix-cluster-csredis` | CSRedis | cluster | 1,000/10,000/100,000 | `VERIFIED_TIMEOUT_BOUNDARY` | -1 |
| `matrix-cluster-easyredis` | EasyRedis | cluster | 1,000/10,000/100,000 | `COMPLETED` | 0 |
| `matrix-cluster-easyhybrid` | EasyHybrid | cluster | 1,000/10,000/100,000 | `VERIFIED_TIMEOUT_BOUNDARY` | -1 |

`VERIFIED_TIMEOUT_BOUNDARY` 只表示该 cell 触发 600 秒窗口，不能推出功能不支持，也不能使用不完整日志宣称该 cell 的能力或性能结果。600 秒窗口覆盖整个 cell，包括预热和正式采样，不是单次 API 调用的超时。完整的 Easy 100,000 项结果来自 [summary.csv](performance-matrix/summary.csv) 的 159 条数据记录；以下只列出 5 个 `COMPLETED` Easy topology/mode 的绝对值。

### Easy 100,000 项绝对指标

| 完整 cell | batchRead P95 ms | batchReadStream P95 ms | scan P95 ms | scanStream P95 ms | remove P95 ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| `matrix-single-easyredis` | 244.1933 | 245.8255 | 491.1817 | 489.5356 | 2,771.4684 |
| `matrix-single-easyhybrid` | 554.7093 | 512.1529 | 727.1779 | 842.3746 | 5,549.9895 |
| `matrix-sharded-easyredis` | 558.9930 | 547.6528 | 653.3491 | 610.9824 | 2,772.4443 |
| `matrix-sharded-easyhybrid` | 745.1986 | 738.8004 | 904.3984 | 899.7631 | 6,088.4580 |
| `matrix-cluster-easyredis` | 2,935.8887 | 2,985.4355 | 959.4512 | 968.0764 | 3,033.4418 |

下表的 `Calls` 是 `NativeCallsMean`，`Par` 是 `ActualParallelismP95`；每个斜杠字段的顺序均为 `batchRead / batchReadStream / scan / scanStream / remove`。`-1` 表示该 phase 没有对应的内部调用或并发观测。

| 完整 cell | Calls | Par |
| --- | --- | --- |
| `matrix-single-easyredis` | `200 / 200 / 200 / 200 / -1` | `1 / 1 / 1 / 1 / -1` |
| `matrix-single-easyhybrid` | `200 / 200 / 200 / 200 / -1` | `1 / 1 / 1 / 1 / -1` |
| `matrix-sharded-easyredis` | `400 / 400 / 201 / 201 / -1` | `2 / 2 / 1 / 1 / -1` |
| `matrix-sharded-easyhybrid` | `400 / 400 / 201 / 201 / -1` | `2 / 2 / 1 / 1 / -1` |
| `matrix-cluster-easyredis` | `98,472.7 / 98,472.7 / 16,513 / 16,513 / -1` | `16 / 16 / 16 / 16 / -1` |

批量和扫描四列的观测 scope 是 `INSTRUMENTED_BATCH_READ_CALLS_AND_FREE_CS_SCAN_CALLS`；`remove` 的 scope 是 `API_CALL_ONLY_INTERNAL_PROVIDER_UNAVAILABLE`。这些 Calls/Par 是已接入探针范围内的绝对观测，不是网络往返数。矩阵其他行中 `stampede` 的 `ObservedQueuedRequestsP95=16` 属于 request/factory 等待，不能解释为 client 内部排队；batch/scan 的 client 队列仍为未知。

已经存在的 [probe-smoke.json](../../../artifacts/ai-tasks/BING-CACHE-TODO-20261005-004/performance-matrix/probe-smoke.json) 只有 CSRedis 单节点 1,000 项、1 轮预热和 3 轮样本，并开启命令/资源探针。它是诊断冒烟证据，不参与上表预算判定。

矩阵的 600 秒终止窗口还带来一项数据边界：终止子进程时其 `finally` 清理逻辑可能没有机会执行，专用 Redis 中可能残留此前随机 benchmark 命名空间的键；后续扫描 cell 可能把这些其他命名空间的扫描成本计入结果。FreeRedis/CSRedis 的 1,000/10,000 项预算在超时 cell 之前已经完整结束，因此上表的二方可比证据不受该残留风险影响。资源矩阵保留原始绝对结果，但不用于宣称 Provider 横向提升或回退；不为消除该限制重跑同一实验。

## 探针范围与未知项

正式 `budget-*.json` 的配置为 `allocationComponents=false`、`commandProbe=false`、`resourceProbe=false`。因此：

- `networkRequests` 的 p50/p95/p99/mean 在批读和扫描阶段均为 `-1`；没有可验证的网络往返次数。
- `maxActualParallelism` 与 `maxQueuedRequests` 均为 `-1`；`requestedConcurrency=16` 只是输入配置，不能当作 Provider 内部实际并发。
- `nativeClientCalls` 在当前正式文件中为 `-1`；`redisCommands` 只是 Redis 命令计数增量，不能替代网络请求数或客户端调用数。
- 正式文件的 `sampledPhasePeakWorkingSetBytes` 为 `-1`。`peakWorkingSetBytes` 来自 `Process.PeakWorkingSet64`，是进程生命周期累计峰值，不是独立阶段或独立样本的资源峰值。
- 正式预算只记录总分配量，未开启分配组成拆分，不能据此把回退归因到某一种临时对象。
- 资源矩阵的 `summary.csv` 对已完成 Easy 批量/流式批量/扫描/流式扫描行接入了 Calls 和实际并发观测；这些 scope 不覆盖真实传输往返或 client 内部排队，不能填补正式预算中的 `networkRequests=-1`。

正式预算中的这些字段必须保持 `-1` 或明确未知，不能使用 Redis 命令数、请求参数或进程累计峰值填补。

## Before / Current / Optimized 可比性

| 版本角色 | 证据 | 可比性 |
| --- | --- | --- |
| Before / 固定旧版 | task003 两份 `keyparse-formal-baseline-*-1k10k.json`，固定提交 `24c93cb1a5355797223637e2d42ac3dce968d7ef` | 与本报告当前正式单节点 1k/10k 数据使用相同运行时、端点、规模、预热/采样配置，可计算上表二方 delta。 |
| Historical Current / task003 历史候选 | task003 的 [CSRedis current](../../../artifacts/ai-tasks/BING-CACHE-TODO-20261004-003/keyparse-formal-current-csredis-1k10k.json) 和 [FreeRedis current](../../../artifacts/ai-tasks/BING-CACHE-TODO-20261004-003/keyparse-formal-current-freeredis-1k10k.json) | 这是上一轮候选的 current 证据；不等同于 task004 优化候选，本报告正式表不把两套 current 数字混用。 |
| Optimized Candidate / task004 当前优化候选 | task004 两份 `performance-matrix/budget-*.json` 及有效的 [candidate-source-identity.json](../../../artifacts/ai-tasks/BING-CACHE-TODO-20261005-004/candidate-source-identity.json) | `candidate-source-identity.json` 是当前源码身份的有效证据；预算 JSON 是该身份下的正式测量，生成时间为 2026-10-05 08:18/08:19 +08。 |
| Intermediate / 中间版 | 只有 task002 的 `*-v2` 或不同方法学的探针产物，没有可重建的中间源码快照 | `BLOCKED_EXTERNAL`。缺的是中间 v2 的可重建源码证据，不是 task004 优化候选的身份；不能拼接不同探针、样本或源码状态计算严格的旧版/中间版/优化版三方比例。 |

`performance-audit.md` 中的四行 keyparse 重算表服务于 task003 的 baseline/current 文件；本报告的正式预算表严格改用 task004 `budget-*.json` 相对同一冻结 baseline 的原始值，因此两张表数值不同是数据集和候选时点不同，不能互相替换。

## TODO 与边界

- **PERF-001｜预算失败。** 批读、扫描和分配预算仍有失败项。需要在保留命名空间、物理键、租约和拓扑隔离语义的前提下提出新的结构性优化，并用同一正式探针复测。
- **PERF-002｜矩阵证据已完成但有边界。** 14 个 cell 均已结束，7 个 `COMPLETED`、7 个 `VERIFIED_TIMEOUT_BOUNDARY`；5 个完整 Easy topology/mode 的 100,000 项绝对值已记录。7 个超时 cell 没有完整 JSON 统计，不能以超时日志宣称能力支持或失败，也不为消除该边界重跑相同实验。
- **PERF-003｜观测范围仍有缺口（DEFERRED）。** 批量路径和 FreeRedis/CSRedis SCAN 的 command probe 已接入，并在资源矩阵中测量；仍缺少真实传输往返、client 内部队列，以及 Easy SCAN（StackExchange `KeysAsync` 内部）的底层分页派发观测。Redis 命令数不能替代这些指标。
- **PERF-004｜工作集解释边界。** 阶段 10ms 工作集采样已在 task004 harness 实现，并由 `probe-smoke` 验证；正式预算刻意关闭 `resourceProbe`，因此预算 JSON 不含该观测。剩余问题是采样峰值不是 OS 精确峰值，且尚未纳入同一预算对照；若作为资源验收依据，需要重新定义口径，不能把这项已实现能力列为未实现 TODO。
- **PERF-005｜缺少中间源码证据。** 在获得可重建的中间候选源码和同探针产物前，严格三阶段比较保持 `BLOCKED_EXTERNAL`。
- **PERF-006｜旧版无等价流式 API。** 流式路径只能报告当前绝对值，历史版回退比较为 `NOT_APPLICABLE_BASELINE_API_MISSING`。
- **PERF-007｜旧版无等价 Cluster/分片构造。** 当前拓扑能力没有合法同构历史 baseline，不能计算回退百分比。

## 证据索引

- 任务计划：[plan.md](plan.md)
- 执行记录：[execution.md](execution.md)
- 性能证据审计：[performance-audit.md](performance-audit.md)
- 矩阵状态：[performance-matrix/run-status.json](../../../artifacts/ai-tasks/BING-CACHE-TODO-20261005-004/performance-matrix/run-status.json)
- 矩阵汇总：[performance-matrix/summary.csv](performance-matrix/summary.csv)
- 当前正式预算：[performance-matrix/budget-csredis.json](../../../artifacts/ai-tasks/BING-CACHE-TODO-20261005-004/performance-matrix/budget-csredis.json)、[performance-matrix/budget-freeredis.json](../../../artifacts/ai-tasks/BING-CACHE-TODO-20261005-004/performance-matrix/budget-freeredis.json)
- task003 冻结基线：[CSRedis baseline](../../../artifacts/ai-tasks/BING-CACHE-TODO-20261004-003/keyparse-formal-baseline-csredis-1k10k.json)、[FreeRedis baseline](../../../artifacts/ai-tasks/BING-CACHE-TODO-20261004-003/keyparse-formal-baseline-freeredis-1k10k.json)

# 缓存性能证据审计

<!-- AI_AUDIT_STATUS: PARTIAL -->
<!-- AI_TASK_ID: BING-CACHE-TODO-20261005-004 -->
<!-- AI_AUDIT_DATE: 2026-10-05 -->

本记录只审计既有性能证据，不重跑基准，不修改原始 JSON。审计输入为固定旧版 `24c93cb1a5355797223637e2d42ac3dce968d7ef`、当前版、.NET 8.0.30、Redis 7.4、`127.0.0.1:52800`、1,000/10,000 项、2 轮预热和 10 轮正式样本。相对回退按：

```text
(currentP95 / baselineP95 - 1) * 100
```

## 已完成

- [x] 使用四份 `keyparse-formal-{baseline,current}-{csredis,freeredis}-1k10k.json` 重新计算命中读指标。
- [x] 将报告中的命中读列改为 `results[].hitRequestLatencyMs.p95`，不再使用包含 100 次请求总耗时的 `phases.hit.elapsedMs.p95`。
- [x] 记录 `Process.PeakWorkingSet64` 是进程生命周期累计峰值，不能解释为单个阶段或单轮独立峰值。
- [x] 保留原始 JSON 不变，并在性能报告中保留原始文件、运行时、TFM 和候选身份。

## 重新计算结果

| Provider | 规模 | 批读 P95 | 扫描 P95 | 批读平均分配 | 扫描平均分配 | 命中读请求 P95 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| CSRedis | 1,000 | +10.1% | +12.7% | +162.3% | +116.8% | -8.8% |
| CSRedis | 10,000 | +41.7% | +23.6% | +182.1% | +126.4% | -0.5% |
| FreeRedis | 1,000 | +37.9% | +34.0% | +68.9% | +71.4% | +4.6% |
| FreeRedis | 10,000 | +39.3% | -41.4% | +73.1% | +73.5% | -11.6% |

性能预算为批读/扫描 P95 不超过 20%、分配量不超过 30%、命中读请求 P95 不超过 10%。因此，命中读请求 P95 四组均通过；批读、扫描和分配量仍存在失败项，不能将本候选标记为预算通过。

## Open Actionable

- [ ] **PERF-001｜FIX-007 预算失败。** 需要在保留命名空间、物理键、租约和拓扑隔离语义的前提下提出新的结构性优化，并用同一探针重新确认。已完成的物理键解析、批量 JSON 解析和局部候选不能满足全部批读/扫描/分配预算；同一假设不再重复微调。

- [ ] **PERF-002｜正式矩阵不完整。** 100,000 项完整重复采样、Cluster 多规模正式性能矩阵，以及 EasyCaching Redis/Hybrid 多拓扑正式矩阵仍缺失。EasyCaching 的 100 项多拓扑文件属于冒烟证据，不能替代预算数据。

- [ ] **PERF-003｜缺少网络和 Provider 内部并发观测。** 现有 JSON 的 `networkRequests`、`maxActualParallelism` 和 `maxQueuedRequests` 在批量/扫描阶段为 `-1`。需要可验证的客户端请求计数和 Provider 内部派发观测，不能用 Redis 命令数或请求参数推断。

- [ ] **PERF-004｜峰值工作集探针粒度不足。** 当前 `peakWorkingSetBytes` 来自进程级累计峰值。若该指标作为资源验收依据，需要改为隔离进程或阶段级采样，并重新定义样本和预算口径。

## Blocked External

- [ ] **PERF-005｜严格三方对照缺少中间版源码。** 上一阶段只有 `*-v2.json` 和不同方法学的探针，没有可重建的中间候选源码快照。没有该快照，无法在同一探针、同一环境下计算“旧版/中间版/优化版”的严格三方比例；不得用不同方法学数据拼接 before/after。

## Not Applicable

- [x] **PERF-006｜旧版无等价流式 API。** 历史版对应 `batchReadStream` 和 `scanStream` 标记为 `NOT_APPLICABLE_BASELINE_API_MISSING`，只能报告当前版绝对值。

- [x] **PERF-007｜旧版无等价 Cluster/客户端分片构造。** 当前 Cluster 和双分片样本没有合法的同构历史 baseline，应作为当前能力和资源观察数据，不计算回退百分比。

## Verified Boundary

- [x] **PERF-008｜单节点 1,000/10,000 项的预算边界已确认。** 固定样本和同一探针下，命中读请求 P95 在预算内；批读、扫描或分配量至少一项超出预算。该结论足以证明当前候选不能通过本阶段性能门禁，不需要通过重复同假设采样改变结论。

## Accepted Limitations

- [x] Redis `INFO stats.total_commands_processed` 只能作为命令增量，不能等同于网络往返次数。
- [x] 10 个正式样本的 P99 只作描述，不作为性能预算判定依据。
- [x] Cluster 新增跨主节点视图校验的当前版绝对耗时可以记录，但在没有等价旧版拓扑时不能解释为回退。

## 证据位置

- 主报告：[performance.md](../BING-CACHE-PERF-STREAM-20261004-002/performance.md)
- 探针：[Program.cs](../BING-CACHE-PERF-STREAM-20261004-002/bench/Program.cs:212)
- 原始文件：`ai_docs/tasks/BING-CACHE-TODO-20261004-003/keyparse-formal-{baseline,current}-{csredis,freeredis}-1k10k.json`
- 运行字段说明：[bench/README.md](../BING-CACHE-PERF-STREAM-20261004-002/bench/README.md)

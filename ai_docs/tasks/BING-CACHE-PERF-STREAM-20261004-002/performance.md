# 缓存批量路径性能证据

> 本报告引用的 JSON 产物为本地归档，位于 `artifacts/ai-tasks/`，不随普通 Git clone 分发。

## 状态

```text
PERFORMANCE_EVIDENCE_STATUS: PARTIAL_BUDGET_FAILED
RESOURCE_EVIDENCE_STATUS: SINGLE_AND_TWO_SHARDS_MEASURED
THRESHOLD_STATUS: TASK_ACCEPTANCE_BUDGET_APPLIED
```

用户批准的计划把批量读与扫描 P95 回退上限设为 20%、分配量回退上限设为 30%、命中读 P95 回退上限设为 10%。本任务按这些数值判定任务验收，不代表批准生产发布。原始数据在 `bench/formal-*.json`；每个单节点结果均为 2 轮预热和 10 轮正式样本，旧版和优化版使用相同端点、.NET 8、输入规模和探针代码。历史版本固定为 `24c93cb1a5355797223637e2d42ac3dce968d7ef`。测试 Redis 为本机隔离的 Redis 7 容器。

| Provider | 规模 | 批量读 P95 回退 | 扫描 P95 回退 | 批量读平均分配回退 | 扫描平均分配回退 | 命中读 P95 回退 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| FreeRedis | 1,000 | +34.4% | +23.2% | +218.4% | +184.2% | +7.2% |
| FreeRedis | 10,000 | +68.7% | +59.9% | +226.2% | +187.6% | -1.5% |
| CSRedis | 1,000 | +37.3% | +27.4% | +463.6% | +470.5% | +5.3% |
| CSRedis | 10,000 | +45.5% | +32.2% | +510.9% | +500.4% | +8.1% |

分配回退按 `allocatedBytes.mean` 计算；若改用 `allocatedBytes.p95`，四组批量读与扫描仍全部超过 30% 上限。批量读、扫描及分配预算均未达到；命中读 P95 在本次样本中达到 10% 上限。旧版与优化版物理键长度、命名空间隔离和租约契约不同，因此这些数字是端到端成本，不能归因于本阶段流式实现，也不能删除隔离功能来追逐数字。上一阶段的 `BING-CACHE-HARDENING-20261004-001/bench/*-v2.json` 记录了中间版本，但探针、预热和样本数不同，只能作为历史描述，不能与本任务结果计算严格的三方回退比例。

在上述正式矩阵之后，CSRedis 扫描路径移除了验证后的物理键重复生成。`bench/diagnostic-current-csredis-no-rehash-1k.json` 使用同样的 2 轮预热和 10 轮样本：扫描平均分配从正式矩阵的 7.07 MB 降为 5.86 MB，相比固定旧版 1.24 MB 仍回退约 373%；扫描 P95 为 15.10 ms，未较前一轮的 13.51 ms 改善。该局部变更保留为分配优化证据，但不改变预算失败结论；表格为此变更前的完整 1k/10k 对照，不能冒充最终候选的全部指标。

第二个诊断候选把 CSRedis 流式 MGET 的原始值类型从 `byte[]` 改为 `string`，`bench/diagnostic-current-csredis-string-mget-1k.json` 记录同条件 10 轮样本。批量/扫描平均分配分别为 5.18/5.88 MB，P95 分别为 7.62/15.87 ms，均未优于前一个候选；该读取策略改动已撤回。两个局部实验没有使 FIX-007 接近预算，按 No Progress Guard 停止重复同类实验。

## FIX-007 结构性诊断与复测（2026-10-04）

`bench/diagnostic-components-*.json` 增加可选 `--allocation-components=true` 诊断阶段。在 1k 项中，CSRedis 的物理键生成、原始 MGET 和逐项 JSON 解码平均分别分配约 0.99/1.27/2.71 MB；FreeRedis 对应约 0.99/2.01/2.66 MB。将同一 MGET 响应的 JSON 片段组成有界数组，单次解码约分配 0.11-0.14 MB。这是本轮批量解析改动的结构性依据；诊断阶段仅处理基准整数值，不能代替生产契约测试。

生产实现对 CSRedis 的每批原始值使用一次 JSON 数组解析；FreeRedis 只在 `JsonConvert.DeserializeObject` 委托上使用该路径，自定义协议继续逐项解析。未命中与已缓存 `null` 分别保留 `Hit` 状态，非空值类型借助可空数组恢复缺失位置的 `default`。两个 Provider 的批量契约、跨批异常、取消、10k List/Stream 一致性和双分片测试通过。

固定旧版提交、同一隔离 Redis `127.0.0.1:52784`、.NET 8、同一探针的 2 轮预热和 10 轮样本原始文件为 `bench/fix007-{baseline,current}-{freeredis,csredis}-1k10k.json`。以下回退按对应旧版计算；分配量为平均值。

| Provider | 规模 | 批读 P95 回退 | 扫描 P95 回退 | 批读分配回退 | 扫描分配回退 |
| --- | ---: | ---: | ---: | ---: | ---: |
| FreeRedis | 1k | +5.9% | -1.3% | +69.6% | +97.0% |
| FreeRedis | 10k | -47.0% | +24.7% | +73.2% | +99.1% |
| CSRedis | 1k | +7.8% | +26.8% | +165.7% | +175.1% |
| CSRedis | 10k | +48.4% | +27.7% | +182.5% | +186.6% |

批量解析显著降低了分配，但四组分配量仍全部超过 30% 预算；FreeRedis 10k 扫描及 CSRedis 扫描/10k 批读仍超过 20% P95 预算。新候选未通过 FIX-007。剩余主要成本来自必须保留的物理键生成、客户端 MGET 返回值和扫描校验；没有新的、已验证可保持隔离语义的结构性方案前，不重复 SHA/返回类型微调，也不宣称性能门禁通过。

双节点客户端分片的当前版完成 1,000 项、2 轮预热、10 轮正式样本：FreeRedis 批量读/扫描 P95 为 9.11/15.74 ms；CSRedis 为 9.34/19.33 ms（自定义路由适配后重测）。两节点命令计数均被监控。历史版没有等价分片构造路径，这些是绝对指标，没有旧版提升百分比。当前版的 `batchReadStream` 和 `scanStream` 阶段真实消费异步枚举；历史版对应状态为 `NOT_APPLICABLE_BASELINE_API_MISSING`。

EasyCaching Redis/Hybrid 当前版也完成单节点 1,000 项、2 轮预热、10 轮正式样本。Redis 的批量读/扫描 P95 为 6.49/8.48 ms，平均分配 1.59/2.06 MB；Hybrid 为 5.82/7.17 ms，平均分配 1.87/2.35 MB。两者的流式批读和扫描均有原始阶段数据。固定历史提交没有等价 EasyCaching 项目，旧版对照为 `NOT_APPLICABLE`，这些绝对值不能判断回退预算是否通过。

## 后续当前版诊断（2026-10-05）

在独立 Redis 7.4 上，公共 `CacheKeyFormat.TryGetLogicalKey` 改为校验编码段与 SHA-256 tag，避免为每个扫描结果重新构造完整物理键。当前版 CSRedis 1k、2 轮预热、10 轮样本中，`keyParse` 平均分配 0.82 MB、P95 0.55 ms，完整扫描平均分配 2.71 MB、P95 13.20 ms；原始文件为 [keyparse-candidate-csredis-1k.json](../../../artifacts/ai-tasks/BING-CACHE-TODO-20261004-003/keyparse-candidate-csredis-1k.json)。先前不同运行的解析诊断约分配 1.51 MB，仅供定位，不能作为正式同环境 before/after。完整扫描的 2.71 MB 仍高于旧版 1k 预算对应水平，`FIX-007` 未通过。

三主三从隔离 Cluster 的四类 Provider 各完成当前版 1k、2 轮预热、10 轮样本。FreeRedis、CSRedis、EasyCaching Redis、EasyCaching Hybrid 的 List 扫描 P95 分别为 390.95、351.12、38.35、40.86 ms；原始文件在 `../BING-CACHE-TODO-20261004-003/cluster-view-current-*-1k.json`。这些样本用于观察新增跨主节点视图校验后的绝对成本；没有同环境历史 Cluster 基线，不能据此计算预算回退。

### 物理键解析候选正式确认

固定旧版 `24c93cb1a5355797223637e2d42ac3dce968d7ef` 与当前版依次使用隔离 Redis 7.4 `127.0.0.1:52800`、.NET 8.0.30、同一 `Program.cs`、1k/10k 项、2 轮预热和 10 轮样本。四份原始文件为 `../BING-CACHE-TODO-20261004-003/keyparse-formal-{baseline,current}-{csredis,freeredis}-1k10k.json`；各规模的 `rawSamples` 均为 10 条。当前版 `CacheKeyFormat.cs` 的 SHA-256 为 `9C7CBD7CCB685EFB218C14E87190184FA91001BFA52162ED89CCC45839DA7BF9`，探针 `Program.cs` 为 `B1AD18AEF231FCA9A4782E946E2AF5A01916ACBDF5868507775FB72C28E56E42`。

| Provider | 规模 | 批读 P95 回退 | 扫描 P95 回退 | 批读平均分配回退 | 扫描平均分配回退 | 命中读请求 P95 回退 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| CSRedis | 1k | +10.1% | +12.7% | +162.3% | +116.8% | -8.8% |
| CSRedis | 10k | +41.7% | +23.6% | +182.1% | +126.4% | -0.5% |
| FreeRedis | 1k | +37.9% | +34.0% | +68.9% | +71.4% | +4.6% |
| FreeRedis | 10k | +39.3% | -41.4% | +73.1% | +73.5% | -11.6% |

本表的命中读列按每次请求的 `results[].hitRequestLatencyMs.p95` 重算，未使用 `phases.hit.elapsedMs.p95`。后者是每轮命中阶段中 100 次请求的总耗时，不能作为单次命中请求 P95。按请求口径，四组命中读均在 10% 回退预算内；批读、扫描和分配量的失败结论不变。

解析优化后的 CSRedis 1k 批读和扫描 P95 达到 20% 上限，但 10k 未达到；FreeRedis 除 10k 扫描外的批读/扫描 P95 也未达到。两个 Provider 的 1k/10k 分配量均超过 30% 上限。`FIX-007` 判定仍为 FAIL；本候选已经完成一次诊断与一次正式确认，不再为同一假设重复采样。

## 指标边界

- `redisCommands` 汇总各监控节点的 `INFO stats.total_commands_processed` 增量，不等于网络往返数；客户端没有可靠的公开请求计数器，`networkRequests=-1`。
- 批量和扫描阶段无法从外部探针观察 Provider 内部任务并发，`maxActualParallelism/maxQueuedRequests=-1`，状态为 `API_CALL_ONLY_INTERNAL_PROVIDER_UNAVAILABLE`；同键回源阶段单独记录实际请求和工厂并发。
- 每个 JSON 保留 `rawSamples`、P50/P95/P99、吞吐、分配量、Redis 命令数、工作集、并发和回源次数。10 个样本的 P99 仅作描述，不用于预算判定。`peakWorkingSetBytes` 来自进程级 `PeakWorkingSet64`，是进程生命周期内的累计峰值，不能解释为单个阶段或单轮的独立峰值；需要阶段级峰值时应使用独立进程或明确的工作集采样方案。
- 100,000 项完整重复性能矩阵尚未执行；1,000/10,000 项已明确判定预算不达标，继续高成本矩阵不会改变该结论。三主三从隔离 Cluster 的 FreeRedis/CSRedis 10k、100k 功能一致性测试已通过；Cluster 只有 1k 当前版诊断样本，未完成正式多规模对照矩阵，功能测试耗时不能当作 P95 基准。

## 性能 TODO

### EasyCaching 分组容器候选（2026-10-06）

EasyCaching `CacheManager` 的单节点 Redis/Hybrid 批量读取改为直接使用一次键数组，Cluster 异步路径改用预分配任务数组；没有改变预算拆分、命名空间、拓扑路由、取消或 Hybrid 回填语义。使用本地 Redis `127.0.0.1:6379`、net8.0、1 次预热、3 次样本、1,000 项和相同 CacheBench 探针得到以下方向性结果：

| 阶段 | 改动前 P95(ms) | 改动后 P95(ms) | 变化 | 平均分配变化 | Redis 命令 |
| --- | ---: | ---: | ---: | ---: | ---: |
| batchRead | 4.1404 | 3.7731 | -8.87% | 647,213 → 647,213 B | 2 → 2 |
| batchReadStream | 4.4339 | 3.8465 | -13.25% | 620,320 → 620,184 B | 2 → 2 |
| scan | 6.0422 | 5.6186 | -7.01% | 841,480 → 843,720 B | 5 → 5 |
| scanStream | 6.1353 | 5.6854 | -7.33% | 807,443 → 807,579 B | 5 → 5 |

原始证据为 `../BING-CACHE-TODO-20261006-039/before-easyredis-1k.json` 和 `../BING-CACHE-TODO-20261006-039/after-easyredis-1k.json`。该候选保留为已验证的结构优化，但分配量没有稳定下降，且 EasyCaching 没有固定历史等价版本；它不能关闭 FIX-007，也不替代 FreeRedis/CSRedis 的正式 1k/10k 性能门禁。

### Redis 前缀 MATCH 下推候选（2026-10-06）

FreeRedis、CSRedis 和 EasyCaching 的前缀读取、前缀删除及流式前缀扫描现在把经过转义的逻辑前缀编码到 Redis `SCAN MATCH` 模式中。模式仍包含当前实例命名空间和 32 个哈希标签通配位；返回结果继续执行物理键哈希校验和逻辑前缀校验，避免把服务端过滤当作隔离边界。空前缀、通配符模式和 `Clear` 仍只使用命名空间模式。

在本机 Redis 7、1,000 个目标键全部匹配的密集数据集上，用相同 CacheBench 探针与上一轮当前版结果做了 1 次预热、3 次采样的方向性比较。该数据集没有无关键，不能代表稀疏命名空间，也不用于替代正式旧版基线。

| Provider | 阶段 | P95 变化 | 平均分配变化 | Redis 命令变化 |
| --- | --- | ---: | ---: | ---: |
| FreeRedis | batchRead | +4.03% | -0.39% | 2 → 2 |
| FreeRedis | batchReadStream | +0.70% | -0.05% | 2 → 2 |
| FreeRedis | scan | +6.91% | -0.23% | 5 → 5 |
| FreeRedis | scanStream | +1.67% | -0.22% | 5 → 5 |
| CSRedis | batchRead | +3.40% | -0.55% | 2 → 2 |
| CSRedis | batchReadStream | +1.83% | -0.05% | 2 → 2 |
| CSRedis | scan | +0.90% | -0.45% | 5 → 5 |
| CSRedis | scanStream | +9.24% | -0.42% | 5 → 5 |

原始结果为 `bench/prefix-match-current-freeredis-1k.json` 和 `bench/prefix-match-current-csredis-1k.json`。由于样本是密集匹配且采样轮次较少，这组结果只确认了实现的正确性边界和低分配变化；它没有关闭 FIX-007。需要在同一命名空间加入大量不匹配键后再评估服务端过滤的绝对收益，不能把当前密集结果宣称为性能预算通过。

### 稀疏扫描基准能力与首轮证据（2026-10-06）

基准程序新增 `--scan-noise-count=<非负整数>`。每轮在正式 `write` 阶段前写入 `noise:` 前缀的无关键，目标仍是 `item:` 前缀；噪声准备不计入写入计时，但会写入 workload identity、checkpoint 的 `noiseLogicalPrefix` 和 `noiseCount`。这使 baseline/current 可以复现相同的数据分布，并验证扫描结果仍只包含目标项。

在 Redis 7、.NET 8、目标 1,000、噪声 9,000、1 次预热、3 次样本下，四份原始报告为：

- `bench/sparse-baseline-freeredis-1k.json` / `bench/sparse-current-freeredis-1k.json`
- `bench/sparse-baseline-csredis-1k.json` / `bench/sparse-current-csredis-1k.json`

| Provider | baseline scan P95 | current sparse scan P95 | P95 变化 | baseline 分配均值 | current 分配均值 | 分配变化 | Redis 命令 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| FreeRedis | 62.2321 ms | 23.9105 ms | -61.58% | 2,905,411 B | 4,197,589 B | +44.47% | 21 → 22 |
| CSRedis | 21.4575 ms | 29.3627 ms | +36.84% | 1,295,552 B | 1,987,909 B | +53.44% | 21 → 42 |

该表不能直接作为严格发布预算结论：旧版与当前版的物理键、命令路由和命中语义不同，CSRedis 当前命令数还包含新的路由/校验路径。它能确认噪声数据集真实执行且目标扫描结果正确。当前版 EasyCaching 的稀疏绝对值如下，历史版本没有等价 Provider：

| Provider | scan P95 | scanStream P95 | scan 分配均值 | scanStream 分配均值 | Redis 命令 |
| --- | ---: | ---: | ---: | ---: | ---: |
| EasyCaching Redis | 14.4308 ms | 13.9830 ms | 837,485 B | 807,712 B | 22 |
| EasyCaching Hybrid | 16.7060 ms | 16.5071 ms | 1,641,632 B | 1,626,005 B | 2,022 |

当前版密集数据与稀疏数据的 scan P95 也分别为 FreeRedis 13.0654 → 23.9105 ms、CSRedis 12.4073 → 29.3627 ms；噪声会增加服务端遍历和客户端工作集，说明当前 `MATCH` 下推只减少返回/解析候选，不能消除 Redis 的全库扫描成本。该基准能力已完成，但 `FIX-007` 仍未关闭。

1. `CacheKeyFormat`、批量反序列化及扫描回读已做分配归因和局部优化；下一候选需在保留命名空间、物理键和租约语义下提出新的结构性改进，再用同一探针对 1,000/10,000 项正式复测。当前不能宣称达标。
2. 上一阶段中间版本仅保留 `*-v2.json` 与不同方法学的探针，没有可重建的候选源码快照；当前 Git HEAD 是固定旧版提交，工作区已继续演化。严格旧版/中间版/优化版三方比例目前不可验证，不能将 v2 数字直接相除。若能提供该中间版源码快照，再用同一探针补测；否则只保留历史描述。
3. 隔离三主三从 Cluster 的 FreeRedis/CSRedis 10k、100k 功能矩阵已完成；EasyCaching Redis/Hybrid 的双分片和 Cluster 已各完成 100 项、1 轮预热和 1 轮样本的构造及运行冒烟，结果位于 `bench/smoke-current-easy*-100.json`，不作为预算数据。Cluster 正式基准矩阵、100,000 项重复性能采样及 EasyCaching 多拓扑正式矩阵仍待运行，需保存完整退出状态与原始 JSON。
4. 如需网络往返数和 Provider 内部并发度，增加可验证的客户端侧观测钩子；不能以 Redis 命令数或固定值替代。

运行方式与原始字段见 [bench/README.md](bench/README.md)。

### CSRedis 扫描连接与物理键直读候选（2026-10-06）

Round 3 针对稀疏 CSRedis 扫描中的额外命令和键转换提出两个结构候选。原始值批次已经完成 `CacheBatchBudget` 校验后，解码阶段不再重复遍历；扫描节点的 `ROLE` 只在节点扫描前后校验并复用连接；同步 `GetByPrefix` 使用已经通过命名空间、哈希和逻辑前缀校验的物理键直接 MGET，避免物理键解码后再次哈希编码。异步扫描同样复用节点连接。没有改变物理键格式、命名空间隔离、取消或 Cluster/分片错误传播语义。

候选使用本机 Redis 7、.NET 8.0.30、目标 1,000 项、噪声 9,000 项、1 次预热、3 次样本。`sparse-current-csredis-budget-1k.json` 是预算去重后的 current/current 诊断，`sparse-current-csredis-scanrole-1k.json` 是扫描连接复用候选，`sparse-current-csredis-physical-1k.json` 是最终物理键直读候选；旧版参照为 `sparse-baseline-csredis-1k.json`。

| 阶段 | 旧版 P95 | 最终候选 P95 | P95 变化 | 旧版分配均值 | 最终候选分配均值 | 分配变化 | Redis 命令 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| batchRead | 5.7709 ms | 6.0531 ms | +4.89% | 899,261 B | 1,613,373 B | +79.41% | 1 → 2 |
| scan | 21.4575 ms | 21.4987 ms | +0.19% | 1,295,552 B | 1,933,611 B | +49.26% | 21 → 24 |
| batchReadStream | 不适用 | 5.7585 ms | current 绝对值 | 不适用 | 1,624,107 B | current 绝对值 | 2 |
| scanStream | 不适用 | 21.6718 ms | current 绝对值 | 不适用 | 2,081,600 B | current 绝对值 | 24 |

扫描命令数从上一个候选的 42 降至 24，扫描 P95 从 29.0522 ms 降至 21.4987 ms；物理键直读主要消除了无效键重建路径，3 样本中分配量没有可确认的下降。相对旧版，扫描延迟落在 20% 回退预算内，但批量/扫描分配量仍超过 30% 预算；这组 1k current/baseline 诊断不能关闭 `FIX-007`，也不替代 10k/100k 正式采样。

### 逻辑命令分类并发观测（2026-10-06）

`CacheCommandProbe` 新增按 `CacheCommandKind` 统计最大重叠数，CacheBench 在每个阶段输出 `commandMaximumParallelism`。该字段用于区分同一阶段内 `SingleRead`、`BatchRead`、`Scan`、`SingleWrite` 和 `Delete` 的逻辑调用并发，避免只用一个总最大值掩盖不同 Provider 路径；它不代表网络往返、连接池等待或 Redis 服务端并发。

Release 冒烟报告 `bench/probe-concurrency-easyredis-10-release.json` 使用本机 Redis、10 项、1 次预热、1 次样本和命令探针，写入阶段记录 `SingleWrite` 最大重叠数为 10，字段在阶段汇总及 `rawSamples` 中均存在。该证据只闭合内部逻辑并发的可观测性子项，不改变 `FIX-007` 的性能预算结论，也没有运行新的重型性能矩阵。`networkRequests`、连接池排队和客户端内部未暴露队列仍保持不可观测状态。

### 跨进程协调证据复核（2026-10-06）

独立任务 `BING-CACHE-TODO-20261006-034` 已在当前工作树和本机隔离 Redis `127.0.0.1:6379` 上重新运行 FreeRedis、CSRedis、EasyCaching Redis 的双进程 ProcessProbe。正常同键合并、持有者异常退出接管、回源超时旧结果拒写、主动失租拒写和等待者超时五类场景均通过。后续批量/扫描和诊断改动没有触碰租约协调实现，因此该证据继续有效；它关闭的是协调回归证据 TODO，不代表 Cluster 故障转移或性能预算通过。

### EasyCaching 客户端分片批量派发候选（2026-10-06）

`ShardedRedisCacheManager.SetAllAsync` 与 `RemoveAllAsync` 从逐分片串行刷新改为有界在途批次：待处理任务数达到 `MaxConcurrency` 后等待一个完成，再继续派发；失败路径会观察已经发送的任务，取消只阻止后续派发。批次容器在提交前替换，避免异步任务与后续填充共享可变集合。该结构只影响客户端分片的异步写入和删除，不改变路由、命名空间、分片身份校验或跨分片非事务语义。

本轮使用 `ShardedBatchConcurrencyUnitTest` 验证最大在途数、取消、异常传播和已派发批次等待，net6.0/net8.0 均为 6/6 通过。没有运行前后性能基准：当前没有可重建的同口径旧版两分片基线，且正式 `FIX-007` 预算针对 FreeRedis/CSRedis 批量读和扫描，不能用单元测试结果替代性能证据。因此本候选记录为功能完成、性能待测，不宣称预算通过。下一次性能检查点应在固定双分片拓扑下使用相同批大小、输入规模、预热和采样轮次，记录 P50/P95/P99、分配量、命令数和实际并发后再判断收益。

### FreeRedis 解码层预算校验去重（2026-10-06）

FreeRedis 的 `ValidateRawBatch` 已在 MGET 响应入口完成返回数量和原始负载预算校验；`DeserializeValues<T>` 的所有调用点均接收该入口返回的数组。本轮移除解码层对同一数组的第二次 `CacheBatchBudget.Check`，保留入口超限拒绝和单键读取的同一校验，减少每个批次的一次完整 UTF-8 负载遍历。该候选不改变命名空间、顺序、命中标记、取消或 Cluster/分片路由语义。

`Bing.Caching.FreeRedis` netstandard2.0 Release 构建通过；`CacheResourceBoundaryTest` 与 `CacheBatchAdaptiveTest` 合计 8/8 通过；FreeRedis 集成测试 net6.0/net8.0 可编译但因未启用隔离 Redis 全部门控跳过。本轮没有生成可与固定 baseline 严格比较的 P95/分配 JSON，因此该候选只作为结构修复记录，不能关闭 `FIX-007` 或宣称性能预算通过。

### CacheJsonBatch 容量双遍历去重（2026-10-06）

字符串 JSON 片段的容量阶段改为按原值长度与替换 token 长度的较大值估算，不再提前扫描空白和 `null` token；UTF-8 字节片段改用每项 `Encoding.UTF8.GetMaxCharCount` 上界，不再先 `GetCharCount` 再 `GetChars`。实际替换和解码仍在写入阶段按原规则执行，ArrayPool 临时字符缓冲和最终 JSON 文本契约不变。

公共 `CacheJsonBatchTest` 在 net6.0/net8.0 各 9/9 通过，`Bing.Caching.Tests` 项目级回归在改动前各 164/164 通过，新增长替换 token 回归在双 TFM 各 9/9 通过；公共库、FreeRedis、CSRedis netstandard2.0 构建均通过。当前没有隔离 Redis，因此没有生成本候选的固定 baseline/current P95 或分配 JSON；这项结构优化不能单独关闭 `FIX-007`。

### CacheKeyFormat UTF-8 sizing pass removal（2026-10-06）

`HashContext.ComputeHash` 和 `FormatPhysicalKey` 原先先执行 `Encoding.UTF8.GetByteCount`，随后再次执行 `GetBytes`。本候选改用 `GetMaxByteCount` 安全上界分配池化缓冲，实际字节仍只由一次 `GetBytes` 生成；物理键格式、哈希输入、命名空间隔离和路由键保持不变。它是与 JSON 容量、数组/容器和键解析实验不同的编码热路径结构修复。

`CacheConfigurationAndKeyFormatTest` net6.0/net8.0 各 18/18 通过，加入中文、BMP 和代理项字符的字节兼容回归；`Bing.Caching.Tests` 项目级回归各 165/165 通过；`Bing.Caching`、FreeRedis、CSRedis netstandard2.0 Release 构建均为 0 警告、0 错误。当前没有隔离 Redis，因此没有本候选的固定 baseline/current P95、分配或命令 JSON；不能据此关闭 `FIX-007`，也不把局部契约回归当作性能预算通过。

### Pattern matcher 清理路径（2026-10-06）

模式清理原先每次创建 `Regex`，再对扫描结果逐键调用 `IsMatch`。本候选保留公共 `CreateRegex`，但 Provider 内部和 `CachePattern.IsMatch` 改为一次解析的 glob token matcher，避免正则编译、回溯和每次扫描重复构造。匹配语义仍为完整字符串、`*`、`?`、反斜线转义和尾部反斜线。

`CachePatternTest` 双 TFM 各 18/18 通过，并将 matcher 结果与 `CreateRegex` 结果逐组合对照；四个受影响生产项目 netstandard2.0 Release 构建无警告无错误，EasyCaching mock 模式清理 2/2 通过。当前没有 Redis 集成环境，FreeRedis/CSRedis 模式测试各 3 项门控跳过；本候选没有生成正式 P95/分配 JSON，不能关闭 `FIX-007`。

### Pattern scan MATCH literal-prefix pushdown（2026-10-06）

模式删除现在从已解析 glob 提取第一个通配符前的字面前缀，并复用 `CacheKeyFormat.GetPrefixPattern` 生成带命名空间和 hash-tag 通配位的 Redis `SCAN MATCH` 模式。`*item` 等无安全前缀模式继续使用命名空间模式；所有返回键仍经过物理布局、哈希和完整 glob matcher 校验。该候选只优化模式删除扫描，不改变前缀读取、租约、清理或取消语义。

`CachePatternTest` 双 TFM 各 22/22 通过，公共缓存全量双 TFM 各 174/174 通过，四个生产项目构建通过。当前没有 Redis，因此没有实际 `SCAN MATCH` 命令、P95、分配或命令数 before/after 证据；FreeRedis/CSRedis 模式集成测试各 3 项跳过，不能据此关闭 `FIX-007`。

### Exact literal pattern deletion（2026-10-06）

模式解析结果全部为字面量时，`RemoveByPattern` 现在直接转为单键删除。该路径复用 Provider 的正常物理键生成、租约撤销和 Hybrid 失效通知，不执行命名空间 `SCAN`；转义后的 `*`、`?` 和尾部反斜线仍按字面键处理。通配符模式继续使用前缀 `MATCH` 下推和客户端 matcher。

本轮没有 Redis 环境，因此只记录结构性验证，不生成 before/after P95 或分配 JSON。公共模式测试双 TFM 各 `28/28`，公共缓存项目各 `180/180`；EasyCaching 全量各 `54` 通过、`52` 个外部门控跳过，四个生产项目 `netstandard2.0` 构建通过。正式性能预算 `FIX-007` 仍未关闭。

### Client-sharded exact pattern routing（2026-10-06）

客户端分片适配器现在在入口解析共享 glob matcher。精确字面量模式直接根据物理键哈希标签路由到唯一分片，再调用单键删除；含通配符的模式继续逐分片扫描。该改动减少精确删除的分片广播和无效扫描，不改变命名空间、租约或节点身份校验。

本轮没有可用 Redis 分片环境，因此没有命令数、SCAN 次数、P95 或分配量 before/after 证据。`ShardedIdentityUnitTest` 双 TFM 各 `9/9`，EasyCaching 全量双 TFM 各 `56 passed / 52 skipped / 0 failed`，只能证明路由和取消/身份校验的结构契约；不能关闭 `FIX-007` 或宣称性能预算通过。

### Scan result budget（2026-10-06）

`CacheResourceOptions.MaxScanKeys` 与内部 `CacheScanBudget` 为扫描增加了可选的候选键上限。它限制客户端继续消费和派发的已验证候选键数量，能够保护结果列表、流式消费和清理工作集；它不限制 Redis `SCAN` 遍历整个命名空间的服务端成本，也不提供 P95 或分配收益的证明。默认值 `0` 保持兼容行为。

本轮虽然有本机单节点 Redis 可用于命令契约回归，但没有运行正式基准探针，因此没有新增 P50/P95/P99、分配量或峰值工作集数据。FreeRedis/CSRedis 单节点集成已验证取消后不继续派发目标 MGET，并直接验证 `MaxScanKeys` 超限边界；资源边界测试和双 TFM 项目回归已通过。`FIX-007` 的批量读/扫描性能预算仍为 `OPEN_ACTIONABLE`。客户端分片目前由各物理分片分别执行扫描预算，跨分片总量上限仍是后续 TODO。

### Client-sharded global scan budget（2026-10-06）

`ShardedRedisCacheManager` 现在在一次跨分片的前缀读取、流式读取、模式/前缀删除或 `Clear` 操作中共享一个 `CacheScanBudget`。预算按已验证候选逻辑键累计，不再由每个物理分片分别重置，因此 `MaxScanKeys` 的资源上限与调用方观察到的整次操作一致。超过预算时仍允许前序分片产生部分结果或删除，操作不提供跨分片事务性。

`ShardedScanCancellationUnitTest` 在 net6.0/net8.0 各 `6/6` 通过；`Bing.EasyCaching.Tests` 完整回归在两个 TFM 各 `59 passed / 52 skipped / 0 failed`。这属于资源正确性证据，不是性能证明；没有新增固定拓扑的 P50/P95/P99、分配量、Redis 命令数或网络队列数据，`FIX-007` 仍为 `OPEN_ACTIONABLE`。

### Memory provider key-stream adapter（2026-10-06）

EasyCaching 的通用 `GetAllKeysByPrefixAsync` 契约返回 `Task<IEnumerable<string>>`，非 Redis Provider 可能在任务完成前构造完整键集合。本轮为 `MemoryCacheManager` 注入程序集内部键流委托，异步前缀读取和异步清理按同步键枚举逐项消费，并在每项检查取消；没有委托的通用 Provider 继续走原异步回退。Redis `IDatabase` 原生扫描路径没有改变。

`CacheManagerTests.GetByPrefixStream_UsesInjectedKeyStreamAndHonorsCancellation` 和 `MemoryCacheManager_UsesSynchronousKeyEnumerationForStream` 在 net8.0 精准测试各通过，EasyCaching 项目 net6.0/net8.0 全量各 `61 passed / 52 skipped / 0 failed`，生产 netstandard2.0 构建通过。这是内存驻留和取消语义修复，不是 Redis 性能预算证据；通用第三方 Provider 的异步键流仍是 TODO，`FIX-007` 继续开放。

### Optional provider key-stream contract（2026-10-06）

新增 `Bing.Caching.ICacheKeyStreamProvider` 作为非 Redis Provider 的可选扩展。实现该接口的 Provider 可直接返回 `IAsyncEnumerable<string>`，`CacheManager` 会在物理命名空间过滤前逐项消费并传递取消令牌；未实现者仍回退 EasyCaching 的 `GetAllKeysByPrefixAsync`。该接口解决了适配器没有异步键流扩展点的问题，但不能替第三方 Provider 保证其内部没有列表物化。

`CacheManagerTests.GetByPrefixStream_UsesProviderKeyStreamCapabilityBeforeLegacyFallback` 验证扩展路径优先级；EasyCaching 项目 net6.0/net8.0 全量各 `62 passed / 52 skipped / 0 failed`，`Bing.Caching` 与 `Bing.EasyCaching` netstandard2.0 构建通过。本轮没有新的 Redis 性能样本，`FIX-007` 仍为 `OPEN_ACTIONABLE`。

### Structured scan budget rejection（2026-10-06）

扫描预算超限现在抛出公开的 `CacheScanLimitExceededException`，并携带 `Limit` 与 `Consumed` 属性。它让调用方、日志和监控能够区分资源拒绝与普通缓存故障，并知道异常发生前已经接受的候选逻辑键数量；它不表示 Redis 服务端已经扫描的键数量，也不能推导完整结果或跨分片事务性。异常仍继承 `InvalidOperationException`，旧捕获代码保持兼容。该变更是错误可观测性和部分成功语义的补齐，不是性能收益证明，`FIX-007` 仍保持开放。

客户端分片现在在同一异常上补充 `CompletedShards` 和 `FailedShard`。分片按稳定顺序处理，因此 `CompletedShards` 只列出异常前完整结束的分片；`FailedShard` 可能已经完成部分读取或删除，不能被解释为回滚点。该字段只改善客户端部分成功的诊断，不限制服务端扫描工作，也没有实现可恢复游标或补偿事务。

### EasyCaching prefix decoder fast path（2026-10-06）

EasyCaching Redis/Hybrid 的前缀读取、前缀流和前缀删除现在复用 `CacheKeyFormat.TryGetLogicalKeyWithPrefix`。该入口先比较物理键中规范转义后的逻辑前缀，只有匹配候选才执行 URI 解码、规范性检查和哈希计算；命名空间、物理布局、相似前缀、特殊字符和篡改哈希仍由同一完整校验确认。模式删除和 `Clear` 没有复用该入口，因为它们必须保留完整 glob/命名空间语义。

本轮只完成结构性优化和回归验证，没有运行新的正式基准，因此不能给出 P50/P95/P99、分配量或命令数 before/after。该优化的可验证收益是减少非匹配物理键的解码和哈希工作；Redis `SCAN` 服务端仍可能遍历同一范围，`FIX-007` 继续保持 `OPEN_ACTIONABLE`。

### FreeRedis/CSRedis logical stream prefix fast path（2026-10-06）

FreeRedis 和 CSRedis 的逻辑键流现在在完整解码前比较规范编码前缀。物理键流原本已经使用 `TryMatchLogicalPrefix`；本轮补齐需要同时返回逻辑键的流式路径，避免不匹配键重复承担 URI 解码、规范验证和哈希计算。命名空间、特殊键、哈希校验、取消、预算和拓扑完整性语义没有改变。

本轮没有启用隔离 Redis，因此只有 `netstandard2.0` 构建证据和门控测试的跳过记录，没有生成性能对照样本。该优化不能证明 Redis 服务端 SCAN 遍历成本下降，`FIX-007` 仍保持开放。

### Scan work observability（2026-10-06）

`CacheCommandProbe` 和 CacheBench 本轮新增 `scanPages`、`scanReturnedItems`。FreeRedis/CSRedis 的显式 SCAN 路径可以报告实际返回页数和逐页物理项数；EasyCaching 的 `Keys/KeysAsync` 适配器隐藏底层分页，因此 `scanPages` 为 `-1`，但仍报告已返回物理项数。`scanReturnedItems` 是客户端返回项计数，不等于 Redis 服务端实际遍历键数；两个字段也不代表网络请求数、Provider 内部排队、重定向或失败重试次数。

本轮仅增加观测，不改变扫描过滤、预算、取消或批处理行为。没有可用的 Redis 基准环境，也没有新增旧版/current/优化版的固定采样，因此没有新的 P50/P95/P99、分配量、峰值工作集或命令数结论。服务端总遍历量、隐藏页边界、跨分片失败恢复和 100,000 项矩阵仍为性能 TODO；`FIX-007` 继续为 `OPEN_ACTIONABLE`。

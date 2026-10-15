<!-- AI_REVIEW_STATUS: PASS_WITH_ISSUES -->
AI_TASK_ID: BING-CACHE-TODO-20261005-004
AI_REVIEWED_AT: 2026-10-05T01:58:15.7154591Z

# 独立代码审查

## 状态

```text
IMPLEMENTATION_STATUS: PASS
TEST_STATUS: PASS
PERFORMANCE_EVIDENCE_STATUS: COMPLETE_WITH_BOUNDARIES_BUDGET_FAILED
RESOURCE_EVIDENCE_STATUS: PASS
EXTERNAL_GATE_STATUS: BLOCKED_EXTERNAL
RELEASE_STATUS: BLOCKED
GOAL_STATUS: STOPPED_BLOCKED
OPEN_ACTIONABLE: 0
```

最终门禁复审对照 `plan.md`、更新后的 `execution.md`、`performance.md`、`TODO.md`、冻结的 `candidate-source-identity.json` 及已落盘门禁证据。首轮四个 MUST_FIX/SHOULD_FIX 均保持 `CLOSED`，当前没有 `OPEN_ACTIONABLE`；实现、正确性测试和资源门禁通过。14 个正式性能 cell 已全部终止并形成合法结果，但既定性能预算未通过，严格三方对照又缺少可重建的中间源码，因此代码审查为 `PASS_WITH_ISSUES`，发布保持 `BLOCKED`，Goal 按规则停止在 `STOPPED_BLOCKED`。

下列 Findings 保留首轮修复前的根因和影响，Delta 复审仅更新最终状态及闭环证据。

## Findings

### FIX-001

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 首轮状态：OPEN_ACTIONABLE
- 最终状态：CLOSED
- 问题：普通 EasyCaching 客户端分片在构造每分片管理器时丢失调用方配置的 `CacheResourceOptions`。
- 证据：`framework/src/Bing.EasyCaching/Bing/EasyCaching/ShardedRedisCacheManager.cs:60` 创建 `innerConfiguration` 时只传到 `CacheTopology.SingleNode`，没有传入 `configuration.Resources`；相对地，Hybrid 分片注册在 `CachingServiceCollectionExtensions.cs:181` 显式复用了 `resources: configuration.Resources`。
- 影响：普通分片模式会忽略调用方设置的回源并发、队列和单批原始字节预算。即使使用默认值，每个分片也会得到独立预算对象，使实例总上限扩大为分片数倍，行为与 Hybrid 分片不同。
- 修复目标：普通分片的所有内部管理器共享外层配置的同一个资源预算对象。
- 修复要求：克隆 `CacheConfiguration` 时传入 `configuration.Resources`，并用小预算验证跨分片仍执行一个实例级总准入上限和原始响应预算。
- 验证方式：增加普通 EasyCaching 双分片的配置传播/实际超限测试，运行对应 net6.0/net8.0 项目测试。
- Delta 证据：`ShardedRedisCacheManager.BuildShards` 构造内部配置时传入 `resources: configuration.Resources`；`ShardedRedis_SharesFactoryAdmissionAndPayloadBudgetAcrossNodes` 明确生成路由到不同节点的前两个键，并验证实例级最大工厂并发为 1、32-byte 原始响应预算拒绝 64-byte 值。该专项在 net6.0/net8.0 均通过。

### FIX-002

- 严重程度：HIGH
- 处理要求：MUST_FIX
- 首轮状态：OPEN_ACTIONABLE
- 最终状态：CLOSED
- 问题：三个 Provider 都在进入同键本地 gate 之前占用全局回源准入，破坏“先做进程内同键合并”的契约。
- 证据：FreeRedis 在 `FreeRedisCacheManager.Coordination.cs:111`、`:191`，CSRedis 在 `CSRedisCacheManager.cs:179`，EasyCaching 在 `CacheManager.cs:276`、`:476` 先调用 `CacheFactoryAdmission.Enter/EnterAsync`，随后才 `AddGate/GetGate` 并等待同键 gate。`CacheResourceBoundaryTest` 只直接测试 helper，没有覆盖 Provider 的真实调用顺序。
- 影响：配置 `maxConcurrentFactories=1,maxQueuedFactories=0` 时，第二个同键请求不会合并等待首个结果，而是立即报“队列已满”。大量热点键等待者还会占满全局额度，导致无关的不同键被饿死。
- 修复目标：同键请求先通过稳定 gate 合并并二次读取；只有实际准备参与分布式租约/执行工厂的请求才占用全局准入。
- 修复要求：统一调整三个 Provider 的顺序，保证所有提前返回、超时、取消和异常路径正确释放 gate 与准入许可。
- 验证方式：对 FreeRedis、CSRedis、EasyCaching Redis/Hybrid 使用真实 Provider，设置 `1/0` 预算并并发请求同一键，断言只调用一次工厂且所有等待者得到同一结果；另以不同键验证总并发仍为 1。
- Delta 证据：FreeRedis、CSRedis、EasyCaching 同步/异步路径均先取得同键 gate、二次读取，再通过 `CacheFactoryAdmission` 获取全局许可；gate 在外层 `finally` 释放，准入许可由 `using` 覆盖命中、异常和取消路径。三个真实 Provider 的同键 `1/0` 合并专项及不同键最大并发 1 专项均通过。

### FIX-003

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 首轮状态：OPEN_ACTIONABLE
- 最终状态：CLOSED
- 问题：FreeRedis 和 EasyCaching 的异步续租在确认失租或续租异常后只记录 `lost`，不取消传给回源委托的生命周期令牌。
- 证据：FreeRedis `FreeRedisCacheManager.Coordination.cs:256-273` 仅写入 `LeaseState.Lost`；EasyCaching `RedisLeaseCoordinator.cs:186-202` 只调用 `lost()`。对应工厂仍接收只链接调用方/总超时的令牌（FreeRedis `:218-231`，EasyCaching `:141-150`）。CSRedis 在 `CSRedisCacheManager.cs:263-270` 会调用 `operation.Cancel()`，三 Provider 语义不一致。现有失租测试只在工厂结束后确认提交被拒绝，没有确认工厂令牌及时取消。
- 影响：租约已被显式写删撤销、节点异常或续租失败后，FreeRedis/EasyCaching 的可取消工厂仍可继续执行到自身完成或两分钟默认总超时，占用 gate、准入许可和外部资源；最终虽拒绝写回，仍造成不必要回源和延迟。
- 修复目标：续租明确失败时取消工厂生命周期并及时返回，同时继续保证旧结果不能写回和清理异常优先级不变。
- 修复要求：为失租引入独立生命周期 CTS，避免把内部失租误报成调用方取消；同步委托仍保留不可强制中止边界。
- 验证方式：工厂等待其收到的令牌，测试删除/替换租约后令牌在续租周期内被取消，API 以协调失败结束，随后新回源能成功。
- Delta 证据：FreeRedis `RenewAsync` 在续租返回非 1 或异常时同时标记失租并调用工厂 lifetime CTS 的 `Cancel`；EasyCaching `RedisLeaseCoordinator` 的失租回调同样取消 timeout/lifetime CTS。共享专项实际删除租约后确认工厂令牌在续租周期内取消、API 在 3 秒内失败、晚结果未写回；FreeRedis、CSRedis、EasyCaching Redis/Hybrid 均通过。

### FIX-004

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 首轮状态：OPEN_ACTIONABLE
- 最终状态：CLOSED
- 问题：Hybrid 显式 Set/SetAll 成功后只删除本地层并发布失效，没有维护当前实例本地层，偏离计划中的双层变更语义并造成既有回归。
- 证据：`HybridCacheManager.cs:28-44` 传给 `RedisLeaseCoordinator` 的成功回调只执行 `local.Remove/RemoveAsync` 后发布；`CacheManager.Mutations.cs:5-19` 的协调器写路径没有后续本地写入。既有 `EasyClusterIntegrationTest.HybridCluster_CrossSlotBatchStreamRemoveAndClear_UpdatesDistributedAndLocalLayers` 在 `EasyClusterIntegrationTest.cs:187` 明确验证 `SetAll` 后本地层有值，阶段回归已暴露该断言失败。已批准的 EasyCaching 计划要求 Hybrid 变更同时处理本地层与跨实例失效。
- 影响：显式写入后当前实例必然冷读，批量写入会立即损失 L1 命中；若简单修改测试接受冷层，会掩盖计划行为回退。自行发布的失效消息还可能异步清掉刚回填的本地值，必须处理发布者回环。
- 修复目标：Redis 原子提交和租约撤销仍先成功；失效发布失败仍向调用方报告；成功后当前实例以不超过 Redis 剩余 TTL 的值维护 L1，并避免自身发布回环再次清除。
- 修复要求：使用有界实例标识过滤自身消息或等价机制，不能保存无界消息 ID；本地回填复用原子 GET/PTTL 规则，不推断默认 TTL。
- 验证方式：单节点和 Cluster/分片 Hybrid 覆盖 Set、TrySet、SetAll 同步/异步，断言 Redis 与当前 L1 值一致、TTL 不延长、对端 L1 被失效；发布失败仍抛错且 Redis 已提交的边界明确。
- Delta 证据：`WriteValue/WriteValueAsync` 在 Redis 原子写入和失效发布成功后调用 `ReadHybridBatch/ReadHybridBatchAsync`，以同一次 Lua GET/PTTL 读取回填 L1；`HybridBusFailureTracker` 使用固定随机实例前缀标记发布消息，订阅侧只过滤自身前缀且不保存消息 ID。Cluster 写后 L1 既有断言、跨实例失效、总线失败已提交边界和重连回归均通过。

## 计划验收矩阵

| 任务 | 状态 | 审查结论 |
| --- | --- | --- |
| T01 Hybrid TTL 回填 | PASS | 值/PTTL 同脚本读取，回填扣除耗时，未知/过期 TTL 不回填，Hybrid Memory 禁用随机延长；500ms/1500ms 实机测试已有证据。 |
| T02 回源生命周期 | PASS | 总超时、遗留任务观察、清理异常优先级已实现；三 Provider 的同键合并、失租令牌取消、晚结果拒写专项已有 fresh 证据。 |
| T03 在途回源顺序 | PASS | 显式写删原子撤销同键租约，旧提交被拒绝；Hybrid 成功写入后按剩余 TTL 热填当前 L1，发布失败仍显式传播。 |
| T04 序列化 | PASS | FreeRedis/CSRedis 对复杂对象逐值独立反序列化，单读/List/Stream 覆盖引用元数据、自引用、nullable、root-null、缺失及坏值传播；fresh 双 TFM Provider 回归通过。 |
| T05 Hybrid 总线重连 | PASS | 复用既有 subscriber multiplexer 的 Subscription 连接事件清空 L1；无补发断线更新/删除测试通过。 |
| T06 流式扫描 | PASS | 非快照和提前取消边界已记录；Easy Cluster Redis/Hybrid 10k/100k 功能对照与四条真实 MONITOR SCAN 提前取消路径均有通过记录。 |
| T07 资源预算 | PASS | 同键 gate 位于全局准入之前；不同键工厂并发/排队、单批原始响应预算及普通 EasyCaching 跨分片共享预算均有真实 Provider 证据。 |
| T08 指标口径 | PASS | 命中请求 P95 已改用单请求字段，工作集明确为进程生命周期累计峰值，原始 JSON 未修改。 |
| T09 性能与观测 | PASS | 14 个 cell 均有终态：7 `COMPLETED`、7 `VERIFIED_TIMEOUT_BOUNDARY`、0 `FAILED`；`summary.csv` 含 159 条记录并报告 5 个完整 Easy 100k cell 的绝对统计。证据采集完成，但正式预算结果为 FAIL，真实传输/客户端排队仍按 DEFERRED 记录。 |
| T10 交付 | PASS | 正确性与资源门禁、双 TFM 回归、生产/benchmark 候选身份、性能报告、TODO、符号映射和独立复审均已收口；发布阻塞项已与实现状态分离。 |

## 验证证据

- 运行产物归档入口：[task004 运行产物](../../../artifacts/ai-tasks/BING-CACHE-TODO-20261005-004/)，包括矩阵汇总、测试结果及门控/构建日志。
- `Bing.Caching.Tests`：net8.0 130 pass，net6.0 130 pass。
- `Bing.Caching.FreeRedis.Tests.Integration`：net8.0/net6.0 均 94 pass / 13 skip。
- `Bing.Caching.CSRedis.Tests.Integration`：net8.0/net6.0 均 91 pass / 12 skip。
- `Bing.EasyCaching.Tests`：net8.0/net6.0 均 67 pass / 18 skip；新增普通分片资源专项在两个 TFM 各 1 pass。
- `dotnet build Bing.All.sln --no-restore`：141 warnings / 0 errors；缓存生产项目均产出 netstandard2.0。NU1801/NU1603 仍按既有外部 NuGet/近似版本警告记录。
- 正式性能矩阵：14 个 cell 全部终止，7 `COMPLETED`、7 `VERIFIED_TIMEOUT_BOUNDARY`、0 `FAILED`；固定 600 秒窗口覆盖整 cell，超时 cell 不生成或推断完整统计。`summary.csv` 含 159 条数据记录。
- 门控专项：2 Provider × 2 TFM × 2 模式共 8 份独立 TRX；关闭门控时逐用例统计 24 `NotExecuted`，开启门控时 24 `Passed`、0 失败。VSTest `Counters.notExecuted=0` 未被误用为逐用例执行结论。
- 最终候选身份：`candidate-source-identity.json` 中 47 个生产/benchmark 源文件哈希未变化；严格 UTF-8 检查覆盖 136 个文件，`git diff --check` 通过，仅有既有 CRLF 提示。
- 测试结束后，本任务创建的 data、bus、cluster 三组 Redis 测试容器均已移除。
- 本次最终门禁复审只读取已落盘证据并更新 `review.md`，未重复运行 Redis、编译、测试或性能任务，也未重审未变化的生产源码。

## TODO 分类

### Completed

- T01 Hybrid 剩余 TTL 回填及随机 TTL 关闭。
- T02 回源生命周期、失租取消与 gate 清理。
- T03 显式写删租约撤销、Hybrid 写后本地层与自身消息过滤。
- T04 独立反序列化语义及异常传播。
- T05 总线重连清理与发布失败传播主路径。
- T06 流式扫描功能矩阵及提前取消边界。
- T07 Provider 准入、跨分片共享预算与单批负载预算。
- T08 性能指标口径更正。
- FIX-001、FIX-002、FIX-003、FIX-004 均为 CLOSED。

### Open Actionable

- 无。性能预算失败、超时边界和外部源码缺失均已按证据分类，不启动新的 Review/Fix 循环。

### Blocked External

- 严格“旧版/中间版/优化版”三方对照缺少可重建的中间候选源码快照；现有不同方法学 JSON 不能拼接成 before/after。

### Not Applicable

- 历史版没有等价流式 API、EasyCaching 新适配和 Cluster/客户端分片构造，这些场景只报告当前能力，不计算伪造的旧版回退比例。

### Accepted Limitations

- 同步用户回源委托不能安全强制中止；到期后停止续租并拒绝写回。
- 原始负载预算在 Redis 响应到达后、解码前执行，不能限制客户端已接收的网络缓冲或解码后对象图。
- SCAN 不是快照；并发修改可造成重复/遗漏，提前停止消费不证明完成末尾拓扑校验。
- Hybrid 为最终一致缓存；扫描清理和批量多键操作不承诺事务性。

### Verified Boundaries

- 固定旧版与当前候选的单节点 1k/10k 证据确认：命中请求 P95 在 10% 预算内，但批读/扫描 P95 或分配量仍有超预算项；不得标记性能门禁通过。
- 正式矩阵的 7 个 cell 在预设 600 秒整轮窗口达到 `VERIFIED_TIMEOUT_BOUNDARY`；该窗口包含预热和正式采样，不表示单次 API 超时，也不能推出功能不支持。
- 另外 7 个 cell 为 `COMPLETED`，其中 5 个 Easy topology/mode 含完整 100,000 项绝对统计；这些无同构历史 baseline 的数据不用于伪造回退比例。

### Deferred

- 真实传输往返、客户端内部排队及 Easy `KeysAsync` 底层分页派发观测。
- 按字节自适应拆分重读；本轮只实现单原始响应批次超预算拒绝。
- benchmark 按规模保存中间检查点及 timeout 后残留扫描数据集身份。

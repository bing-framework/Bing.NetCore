# BING-CACHE-TODO-20261007-072：客户端分片批量读取有界并发

## 目标

修复 FreeRedis 与 CSRedis 客户端分片批量读取路径的结构性性能缺口：节点已经分组，但每个节点批次仍串行执行，未使用 `CacheConfiguration.MaxConcurrency`。改为按节点窗口派发有界并发任务，同时保持输入顺序、重复键、缺失/null 命中、取消和失败传播语义。

## 现状与证据

- `framework/src/Bing.Caching.FreeRedis/Bing/Caching/FreeRedis/FreeRedisCacheManager.Stream.cs` 的 `ReadInputBatchAsync` 和 `ReadValuesBatchAsync` 在 `ClientSharded` 分组后使用 `foreach` 逐节点 `MGET`。
- `framework/src/Bing.Caching.CSRedis/Bing/Caching/CSRedis/CSRedisCacheManager.Stream.cs` 的 `FillShardedEntriesAsync` 和 `ReadShardedValuesAsync` 同样逐节点等待。
- 两个 Provider 的 Redis Cluster 路径已经使用 `MaxConcurrency` 窗口和在途任务观察，可复用该模式。
- 这会把跨分片批量读的实际并发固定为 1，增加总等待时间；不改变单节点路径、键路由、批大小或协议。

## 修改范围

### 生产代码

- FreeRedis 流式/批量读取的客户端分片分支：按 `MaxConcurrency` 窗口派发 `ReadRawBatchAsync`，失败或取消时观察已派发任务。
- CSRedis 流式/批量读取的客户端分片分支：按同一窗口派发原始 MGET，完成后按原索引回填。
- 不修改公共接口、物理键格式、Cluster 分组和删除/租约路径。

### 测试与证据

- 在受影响的 Provider 集成测试中补充或更新分片批量读取并发/取消回归；测试保持现有 `RUN_INTEGRATION_TESTS` 门控。
- 使用现有共享契约验证 List/Stream 顺序和命中语义；无法提供双节点环境时只记录外部阻塞，不用单节点替代。
- 更新 TODO、执行记录和生产符号到测试映射。

## 实施步骤

1. 为 072 建立任务状态、TODO 和测试映射，并把该新缺口登记为当前唯一 `OPEN_ACTIONABLE`。
2. 重构 FreeRedis 两个分片读取分支，窗口大小为 `MaxConcurrency`，每窗口内任务按分组原索引回填。
3. 重构 CSRedis 两个分片读取分支，复用现有 `ObservePendingAsync`，取消后停止新窗口和新任务派发。
4. 增加针对窗口并发、顺序/重复键和第二批异常传播的测试；保留真实双节点测试门控。
5. 运行 L1 精准测试、受影响项目 L2/L3 测试和 netstandard2.0 构建；真实双节点缺失时分类为 `BLOCKED_EXTERNAL`。
6. 更新权威索引并完成任务收口。

## 依赖与风险

- 依赖 `MaxConcurrency` 已在 `CacheConfiguration` 中校验为正数；不新增配置或公共 API。
- 客户端分片各节点使用独立 Redis 连接，可以并行发送批次；同一节点同一窗口最多一个批次，避免单节点连接争用。
- 已发送任务在取消后不会撤回；取消只阻止后续派发，和现有异步契约一致。
- 跨节点批次仍可能部分成功；首个异常继续传播，其余已派发任务被观察，不能宣称跨节点事务性。

## 验收标准

- FreeRedis 和 CSRedis 的 ClientSharded 读取路径实际最多同时运行 `MaxConcurrency` 个节点批次，不再固定串行。
- List 与 Stream 保持输入顺序、重复键位置、缺失/default 和 null 命中语义。
- 取消在下一窗口或下一任务前生效，已派发任务被观察且不出现未观察异常。
- 第二批失败只返回已产出的前序结果并传播异常；不把部分结果伪装为完整成功。
- 受影响生产项目 netstandard2.0 构建无警告/错误；本地精准测试通过；真实双节点证据按环境门禁分类。

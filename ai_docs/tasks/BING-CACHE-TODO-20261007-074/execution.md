<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: BING-CACHE-TODO-20261007-074
AI_EXECUTION_FINISHED_AT: 2026-10-07T09:31:45.1193419+08:00
AI_EXECUTION_STARTED_AT: 2026-10-07T01:24:15.318Z

## 任务状态

- 目标：让 EasyCaching 客户端分片前缀 List/Stream 读取使用有界并发，同时保证共享扫描预算和取消/异常语义。
- 完成时间：2026-10-07T09:31:45.1193419+08:00
- 当前状态：本地实现和验证完成；真实双节点证据外部阻塞。

## Change Impact Analysis

- ChangedFiles：`CacheScanBudget`、`CacheScanLimitExceededException`、`ShardedRedisCacheManager`、EasyCaching 分片扫描单元测试、任务文档和权威索引。
- ChangedProjects：`Bing.Caching`、`Bing.EasyCaching`、`Bing.EasyCaching.Tests`。
- ChangedPublicContracts：异常公开属性语义增强；不新增缓存接口方法。
- ChangedRuntimePaths：EasyCaching ClientSharded 前缀 List/Stream 和同步前缀读取。
- ChangedProviders：EasyCaching Redis/分片 Redis；FreeRedis/CSRedis 不变。
- ChangedTFMs：生产 netstandard2.0；测试 net6.0/net8.0。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无；正式性能矩阵复用既有边界。
- ChangedDocsOnly：任务记录和缓存 TODO 索引。
- AffectedDependents：EasyCaching 单元测试、分片集成测试和 `IRedisCache` 消费方。
- RiskLevel：HIGH。并发预算、流式枚举器清理和异常优先级同时变化。

## 实现

- `CacheScanBudget` 使用锁保护候选键/扫描页计数，使用 `AsyncLocal` 绑定当前分片，并记录非连续的已完成分片集合；顺序调用继续支持 `SetShardProgress`。
- `CacheScanLimitExceededException` 增加真实分片索引快照构造路径，`CompletedShards` 按索引升序返回。
- `ShardedRedisCacheManager.GetByPrefixAsync` 在 `MaxConcurrency` 窗口内并发分片扫描，结果按分片索引合并；同步 `GetByPrefix` 通过异步实现等待，避免旧的逐分片同步扫描。
- `GetByPrefixStreamAsync` 为每个在途分片维护一个 `MoveNextAsync`，按完成顺序逐项产出；取消、异常和提前结束时取消共享令牌、观察任务并释放枚举器。
- `GetAll`、写入、删除、租约和 Hybrid 总线路径未修改。

## 验证

### L0 静态检查

- 直接受影响的 `Bing.Caching`、`Bing.EasyCaching` netstandard2.0 构建均为 0 警告、0 错误。
- `git diff --check`：无新增空白错误；仅保留既有 CRLF 转换提示。

### L1 精准测试

- `ShardedScanCancellationUnitTest` net6/net8：各 9/9 通过。
- `CacheResourceBoundaryTest` net6/net8：各 15/15 通过，覆盖并发候选键和扫描页预算。

### L2 受影响项目

- `Bing.Caching.Tests` 完整 net6/net8：各 203/203 通过。
- `Bing.EasyCaching.Tests` 完整 net6/net8：各 94 通过、54 跳过；跳过均为既有 Redis/Cluster/总线环境门控。

### L3 集成

- `EasyShardedIntegrationTest` net6/net8：各发现 6 项，0 失败、6 跳过；未设置两个独立非生产 Redis 节点。
- 不能用单节点结果替代真实跨分片并发、身份变化和部分失败证据。

### L4/L5

- L4 解决方案回归：本轮不执行；改动限定为扫描预算、EasyCaching 分片前缀读取和对应测试。
- L5 正式性能矩阵：不执行；同构旧/中间/优化基线和真实网络观测仍按权威索引边界处理。

## 状态分类

- 已完成：本轮五项 TODO 全部完成；EasyCaching 分片前缀 List/Stream 已使用有界并发，预算保持全局上限。
- `OPEN_ACTIONABLE`：无。
- `BLOCKED_EXTERNAL`：双节点分片、三主三从 Cluster、双进程回源、独立总线和真实 SCAN 页预算回归仍缺环境。
- `BLOCKED_APPROVAL`：NullCache 流式缺失项语义仍待明确。
- `DEFERRED`：分片清理/模式扫描并发、代理背压重试/outbox、网络/客户端排队观测和严格三版性能对照。
- `VERIFIED_BOUNDARY`：正式性能预算仍为 `COMPLETE_WITH_BOUNDARIES_BUDGET_FAILED`；100,000 项超时 cell 保留原结论。
- `ACCEPTED_LIMITATION`：前缀 Stream 跨分片不保证顺序；扫描非快照；已发送命令不可撤销；失败不具备跨分片事务性。

## No-Progress Check

- 结论：`CHANGED`。074 处理了新的本地根因：EasyCaching 分片前缀读取绕过 `MaxConcurrency`，且共享扫描预算不支持并发；本轮完成实现、并发预算测试和两个 TFM 完整单元回归。
- 未重复运行正式性能矩阵，也未将门控跳过写成真实拓扑通过。

## 下一步

- 停止本地前缀读取重复修复；提供双节点环境后重新运行 `EasyShardedIntegrationTest` 关闭外部证据。
- 分片删除、模式清理和 Clear 并发需先确认部分成功与顺序契约，再另立任务。

## 验收记录

待实现和验证后填写 L0-L5、外部阻塞、已完成 TODO 和未完成 TODO。

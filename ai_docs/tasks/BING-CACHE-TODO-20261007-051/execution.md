<!-- AI_EXECUTION_STATUS: PARTIAL -->
AI_TASK_ID: BING-CACHE-TODO-20261007-051
AI_EXECUTION_FINISHED_AT: 2026-10-07T03:46:37+08:00

# 实施执行报告

## Change Impact Analysis

- ChangedFiles：`CacheResourceOptions`、`CacheDeleteBatchBudget`、FreeRedis/CSRedis 删除批次、EasyCaching Redis/Cluster/分片删除路径、缓存 README、公共/Provider/EasyCaching 测试和本任务文档。
- ChangedProjects：`Bing.Caching`、`Bing.Caching.FreeRedis`、`Bing.Caching.CSRedis`、`Bing.EasyCaching` 及对应测试项目。
- ChangedPublicContracts：新增可选资源配置属性 `CacheResourceOptions.MaxBatchCommandBytes`；未修改 `ICache` 删除方法签名。
- ChangedRuntimePaths：显式键删除、批量删除、前缀删除、模式删除、Clear，以及 Cluster 槽位和客户端分片分组后的删除命令派发。
- ChangedProviders：FreeRedis、CSRedis、EasyCaching Redis/Cluster/客户端分片；Hybrid 协调路径保留逐键租约删除。
- ChangedTFMs：生产 `netstandard2.0`；测试 `net6.0`、`net8.0`。
- ChangedBuildPackaging：无新增依赖、无版本和包发布变更。
- ChangedBenchmarkHarness：无；本轮不重复运行已有读/扫描性能基准。
- AffectedDependents：缓存清理调用方及构造 `CacheResourceOptions` 的配置调用方。
- RiskLevel：MEDIUM。删除命令会按 UTF-8 参数预算拆分，但每键删除、命名空间隔离、租约键配对和部分成功语义保持不变。

## 实施内容

- `CacheResourceOptions.MaxBatchCommandBytes` 默认 8 MiB，校验参数名准确；文档说明预算包含数据键与 `:lease` 键参数，不包含 Redis 协议帧。
- `CacheDeleteBatchBudget` 按 UTF-8 字节数和 `BatchSize` 有界切分，保持输入顺序与数据键/租约键配对；单个键对超预算在发送命令前明确抛错。
- FreeRedis、CSRedis 在单节点、Cluster 槽位和客户端分片路由后统一使用预算切分；同步/异步、显式键、前缀/模式和 Clear 共用删除路径。
- EasyCaching Redis/Cluster/客户端分片复用预算；Hybrid 协调路径继续逐键处理，避免破坏租约和失效通知语义。
- 新增公共边界、EasyCaching mock 批次、FreeRedis/CSRedis Redis MONITOR 门控测试，并补充四个包的使用说明和生产符号到测试映射。

## 验证结果

### 精准及回归测试

- `Bing.Caching.Tests` net8.0：189 passed / 0 skipped / 0 failed。
- `Bing.Caching.Tests` net6.0：189 passed / 0 skipped / 0 failed。
- `Bing.EasyCaching.Tests` net8.0：71 passed / 52 skipped / 0 failed。
- `Bing.EasyCaching.Tests` net6.0：71 passed / 52 skipped / 0 failed。
- FreeRedis/CSRedis `DeleteByteBudgetTest` net6.0/net8.0：各 0 passed / 2 skipped / 0 failed；测试程序集编译成功，Redis 门控关闭。

### 生产构建与格式

- `Bing.Caching`、`Bing.Caching.FreeRedis`、`Bing.Caching.CSRedis`、`Bing.EasyCaching` `netstandard2.0 Release`：均 0 warning / 0 error。
- `git diff --check`：无空白错误；仅报告工作区既有 CRLF/LF 转换提示。

## TODO 分类

### Completed

- 删除命令 UTF-8 参数预算、数量预算和租约键配对。
- FreeRedis、CSRedis、EasyCaching Redis/Cluster/分片删除路径接入。
- 参数边界、批次切分、EasyCaching mock 行为和 Provider MONITOR 门控测试。
- 文档、TODO 和生产符号到测试映射。

### Open Actionable

- 无。本轮同范围内没有仍可通过当前工作区修改解决的实现缺口。

### Blocked External

- 需要非生产 Redis 单节点并开启 `RUN_INTEGRATION_TESTS=true`，才能验证真实服务端收到的 `DEL` 数量、UTF-8 长键拆分和兄弟命名空间哨兵保护。
- 需要三主三从 Cluster、双节点客户端分片和 Hybrid 总线环境，才能验证跨槽/跨节点批次、拓扑变化及失效传播下的删除行为。

### Verified Boundary

- 单个数据键及对应 `:lease` 键参数超过 `MaxBatchCommandBytes` 时，本地测试确认会在命令派发前抛错。
- 公共预算 helper 已确认按 UTF-8 字节数和 `BatchSize` 保持顺序切分。

### Deferred / Accepted Limitation

- 当前预算只计算 Redis 命令参数，不计算 RESP 协议帧、客户端队列、TLS/网络开销；若需要服务端硬上限，需要额外协议开销模型和安全余量。
- 本轮没有新增旧版/当前版/优化版性能对照；命令数、网络往返、实际并发和工作集仍沿用现有性能 TODO，不能由本轮构建或单元测试推导收益。
- 删除仍是非事务性的多批次操作；部分批次成功后失败会向调用方抛异常，不回滚已经发送的命令。

## 计划偏差

无。未修改物理键格式、租约协议、Cluster/分片路由契约或性能预算；未提交、推送或发布。

# 扫描删除批量化计划

## 目标

将 FreeRedis 和 CSRedis 的前缀删除、模式删除及命名空间清理从逐键 `DEL` 改为有界批量删除，减少扫描清理的 Redis 命令数量，同时保持现有命名空间隔离、租约键清理、Cluster/客户端分片路由、取消和部分成功语义。

## 当前确认的缺口

- FreeRedis 的 `ForEachKeyAsync` 对每个扫描结果调用 `RemovePhysicalAsync`；同步扫描路径也逐键调用 `RemovePhysical`。
- CSRedis 的 `RemoveMatchingAsync` visitor 对每个逻辑键调用 `RemoveAsync`；同步 `RemoveMatching` 同样逐键删除。
- EasyCaching 已有 `RemovePhysicalBatch`/`RemovePhysicalBatchAsync`，不属于本轮修改范围。

## 实施范围

### P1：共享删除批次规则

- 每个批次限制为配置的 `BatchSize` 个数据键，展开后的数据键和 `:lease` 键共同计入命令参数预算。
- 单节点按实际客户端分组；客户端分片按路由客户端分组；Redis Cluster 按 hash slot 分组，绝不发送跨槽多键 `DEL`。
- 每个批次最多按 `MaxConcurrency` 个 Redis 命令在途；取消后停止收集和派发新批次，已发送命令不回滚。
- 继续通过 `CacheCommandProbe.Delete` 记录逻辑删除调用，异常按现有调用方语义传播。

### P2：FreeRedis

- 为同步和异步扫描访问提供批次缓冲。
- 批次刷新时按 `ClientForPhysicalKey` 分组；Cluster 客户端再按槽位分组。
- 批量删除每个数据键及其租约键，保留拓扑完整性检查和扫描预算。

### P3：CSRedis

- 为同步和异步 `VisitAsync`/`RemoveMatching` 提供批次缓冲。
- 批次刷新时按扫描节点客户端分组；Cluster 按槽位分组。
- 保留访问主节点、拓扑变化显式失败、取消和扫描预算语义。

### P4：回归证据

- 增加或更新 FreeRedis/CSRedis 扫描删除测试，覆盖前缀、模式、`Clear`、租约键清理、取消和特殊键。
- 在实机门控开启时断言批量删除结果和命令聚合；门控关闭时仅记录编译/跳过。
- 运行受影响 Provider 的精准测试、生产 `netstandard2.0` 构建和 `git diff --check`。

## 非目标

- 不修改 `ICache`、Provider 公共签名、物理键格式或租约协议。
- 不把扫描改为原子操作，不承诺并发写入期间最终为空。
- 不重复已确认的批量读/扫描性能基准，不调整性能预算。
- 不伪造 Redis、Cluster、分片或 Hybrid 外部环境结果。

## Change Impact Analysis

- ChangedFiles：FreeRedis/CSRedis 扫描删除实现、对应精准/集成测试、本任务文档和生产符号映射。
- ChangedProjects：`Bing.Caching.FreeRedis`、`Bing.Caching.CSRedis` 及其集成测试项目。
- ChangedPublicContracts：无。
- ChangedRuntimePaths：同步/异步前缀删除、模式删除、`Clear`、数据键与租约键批量删除。
- ChangedProviders：FreeRedis、CSRedis。
- ChangedTFMs：受影响测试的 net6.0/net8.0；生产项目 netstandard2.0。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无。
- AffectedDependents：依赖扫描清理和 `CacheCommandProbe` 的测试调用方。
- RiskLevel：MEDIUM。

## 验收标准

1. 扫描删除不再为每个数据键单独发一个 `DEL(data, lease)` 命令。
2. Cluster 批次始终按槽位分组，客户端分片批次始终按客户端分组。
3. 数据键和对应租约键均被删除，命名空间外哨兵键不受影响。
4. 取消、扫描预算超限、拓扑变化和批次异常仍按既有语义传播。
5. 受影响精准测试、生产构建和格式检查通过；门控关闭时不把跳过记为实机通过。

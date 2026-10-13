# RemoveAll 批量删除优化计划

## 目标

将 FreeRedis 和 CSRedis 的 `RemoveAll`/`RemoveAllAsync` 从逐键删除改为复用现有的有界物理键批量删除路径，减少 Redis `DEL` 命令数量，并保持命名空间、租约键、Cluster 槽位、客户端分片路由、取消和部分成功语义。

## 现状与证据

- FreeRedis `RemoveAll` 同步路径逐项调用 `RemovePhysical`，异步路径逐项创建 `RemovePhysicalAsync` 任务。
- CSRedis `RemoveAll` 同步路径逐项调用 `Remove`，异步路径逐项调用 `RemoveAsync`。
- 两个 Provider 已有 `RemovePhysicalBatch`/`RemovePhysicalBatchAsync`，扫描删除已使用该路径；它们会把每个数据键展开为数据键和 `:lease` 键，并按客户端或 Cluster 槽位分组。
- EasyCaching 和客户端分片包装器已经具备批量 `RemoveAll` 路径，本轮只修改 FreeRedis/CSRedis。

## 实施范围

### P1：FreeRedis

- 同步 `RemoveAll(IEnumerable<string>)` 每次收集 `BatchSize` 个物理键后调用 `RemovePhysicalBatch`，末批立即刷新。
- 异步 `RemoveAllAsync(IEnumerable<string>, CancellationToken)` 每次收集 `BatchSize` 个物理键后调用 `RemovePhysicalBatchAsync`；取消只阻止后续批次，已发命令继续被观察。
- 保留输入只枚举一次、空输入无 Redis 访问和重复键原语义。

### P2：CSRedis

- 使用同一批次规则接入已有物理删除分组路径。
- 保留单节点、Cluster、客户端分片路由和 `MaxConcurrency` 限制。

### P3：回归证据

- 两个 Provider 各增加门控集成测试：5 个键、`BatchSize=2`，通过 `MONITOR` 和 `PING` 排空屏障确认异步 `RemoveAllAsync` 产生 3 个当前命名空间 `DEL`，并验证数据键和租约键均删除。
- 更新生产符号到测试方法映射和执行记录。
- 运行双 TFM 精准测试、两个生产项目 netstandard2.0 构建和 `git diff --check`。

## 非目标

- 不修改 `ICache` 公共签名、物理键格式、租约协议或删除原子性保证。
- 不为删除命令新增字节预算；沿用现有 `BatchSize` 和已配置并发限制，字节预算作为后续设计 TODO 记录。
- 不重复批量读/扫描性能基准；只验证命令聚合和结果正确性。

## Change Impact Analysis

- ChangedFiles：FreeRedis/CSRedis `RemoveAll` 实现、对应集成测试和本任务文档。
- ChangedProjects：`Bing.Caching.FreeRedis`、`Bing.Caching.CSRedis` 及其集成测试项目。
- ChangedPublicContracts：无。
- ChangedRuntimePaths：同步/异步显式键删除。
- ChangedProviders：FreeRedis、CSRedis。
- ChangedTFMs：测试 net6.0/net8.0；生产 netstandard2.0。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无。
- AffectedDependents：调用 `RemoveAll`/`RemoveAllAsync` 的缓存清理路径。
- RiskLevel：MEDIUM。删除命令聚合改变命令边界，但保留逐键数据与租约删除及部分成功语义。

## 验收标准

1. 单节点 5 个键、`BatchSize=2` 的异步批量删除只产生 3 个命名空间 `DEL`。
2. 数据键和对应租约键均被删除，输入外命名空间不受影响。
3. 空输入、重复键、取消和异常传播不回退到无界任务或逐键派发。
4. Cluster/客户端分片继续按已有物理删除分组，不发送跨槽多键 `DEL`。
5. 受影响测试项目可编译，门控关闭时明确 Skip，不把 Skip 计为实机通过。

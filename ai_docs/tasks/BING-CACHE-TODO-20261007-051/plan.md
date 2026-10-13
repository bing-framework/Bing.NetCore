# 删除命令字节预算与有界批次计划

## 目标

实现缓存删除命令的 UTF-8 参数字节预算，避免长物理键或租约键后缀使单个 `DEL` 命令超过配置资源预算。保留现有按键数量、Cluster 槽位、客户端分片、取消和部分成功语义。

## 当前缺口

- FreeRedis、CSRedis 和 EasyCaching 的删除批次目前主要按 `BatchSize` 限制。
- 每个数据键会展开为数据键和 `:lease` 键；逻辑键较长时，命令参数字节数可能随数量增长而超过合理上限。
- 现有 `MaxBatchPayloadBytes` 只描述 Redis 原始读响应，不能复用其语义作为删除请求预算。

## 实施范围

### P1：配置与共享预算

- 在 `CacheResourceOptions` 增加可选的 `MaxBatchCommandBytes`，默认与现有 8 MiB 资源预算同量级，保持已有构造调用兼容。
- 新增内部 `CacheDeleteBatchBudget`，按 UTF-8 字节数计算每个数据键及其 `:lease` 参数；同时遵守 `BatchSize`。
- 单个数据键及租约键对超过预算时抛出明确异常，避免无限拆分或静默超限。

### P2：Redis Provider 接入

- FreeRedis 和 CSRedis 的显式删除、前缀删除、模式删除及 `Clear` 在客户端/槽位分组后继续按字节预算切分。
- 同步和异步路径使用同一切分规则；异步路径保留 `MaxConcurrency`、取消停止新命令和已派发命令观察语义。
- 不改变物理键格式、租约协议、命名空间过滤、拓扑完整性校验或部分成功语义。

### P3：EasyCaching 路径

- Redis/Cluster/分片删除路径复用同一预算规则。
- Hybrid 带租约协调的逐键删除保持逐键语义；不把协调路径强行合并成跨键命令。

### P4：测试与文档

- 公共单元测试覆盖 Unicode、租约后缀、恰好达到预算、跨预算切分、单键超限和空输入。
- FreeRedis/CSRedis/EasyCaching 增加门控命令聚合测试，确认长键按字节拆分且命名空间外哨兵不受影响。
- 更新生产符号到测试方法映射、TODO 和 execution；不重复已完成的批量读/扫描性能基准。

## 非目标

- 不修改 `SetAll` 的每键 TTL/租约写入语义。
- 不把删除请求字节数误记为网络往返数；实际传输和客户端排队仍由性能观测 TODO 负责。
- 不改变公共 `ICache` 删除 API，不承诺跨批次事务性。

## Change Impact Analysis

- ChangedFiles：`CacheResourceOptions`、共享删除预算 helper、FreeRedis/CSRedis/EasyCaching 删除路径、相关单元/门控测试及任务文档。
- ChangedProjects：`Bing.Caching`、`Bing.Caching.FreeRedis`、`Bing.Caching.CSRedis`、`Bing.EasyCaching` 及受影响测试项目。
- ChangedPublicContracts：新增可选资源配置属性；不改 `ICache` 方法签名。
- ChangedRuntimePaths：显式键删除、扫描删除、Clear、Cluster/分片分组后的批次派发。
- ChangedProviders：FreeRedis、CSRedis、EasyCaching Redis/Hybrid。
- ChangedTFMs：生产 `netstandard2.0`；测试 `net6.0`、`net8.0`。
- ChangedBuildPackaging：无新增依赖。
- ChangedBenchmarkHarness：无；沿用已有性能证据。
- AffectedDependents：配置构造调用方和所有缓存清理调用方。
- RiskLevel：MEDIUM。删除命令边界变化，但每键删除和部分成功语义保持不变。

## 验收标准

1. 默认配置下短键行为和既有批量命令数不变。
2. 长 Unicode 物理键会在字节预算达到前切分，数据键和 `:lease` 键始终在同一删除子批次。
3. 单个数据键及租约键对超过预算时明确抛错，不发送超预算命令。
4. Cluster/分片不会产生跨槽或跨节点删除命令。
5. 取消只停止后续批次，已派发命令仍被观察；失败不伪装成全部成功。
6. 受影响生产项目构建、精准单元测试和 `git diff --check` 通过；Redis 门控关闭时明确记录 Skip。

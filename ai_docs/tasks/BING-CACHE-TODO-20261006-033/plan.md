# Redis 批量读取临时数组复用计划

## 目标

处理当前缓存性能 TODO 中一个仍可执行且与已撤回 MGET/序列化候选不同的分配根因：FreeRedis、CSRedis 的 `GetAllAsync` 已经由 `CacheKeyFormat.GetPhysicalKeys` 生成 `string[]`，随后 `ReadValuesBatchAsync` 在单节点路径无条件调用 `ToArray()`，每个批次再次复制键引用。

本轮只复用已经拥有的数组，不改变 Redis 命令、序列化、命名空间、租约、批大小、顺序、取消或失败传播语义。正式性能预算仍保持开放，不以局部优化宣称 FIX-007 通过。

## 变更

- FreeRedis `ReadValuesBatchAsync` 单节点读取使用已有 `string[]`；前缀扫描传入 `List<string>` 时继续创建客户端所需数组。
- CSRedis 同步处理。
- 用一个内部小 helper 在 `IList<string>` 为数组时直接返回，否则按原语义复制，避免复制逻辑分散。
- 不修改公共接口、物理键格式和 `CacheBatchAdaptive` 的拆分策略。

## Change Impact Analysis

ChangedFiles: 两个 Provider 的 Stream partial、任务文档。
ChangedProjects: `Bing.Caching.FreeRedis`、`Bing.Caching.CSRedis`。
ChangedPublicContracts: 无。
ChangedRuntimePaths: 两个 Provider 单节点 `GetAllAsync` 的批量 List 读取；客户端分片/Cluster 和前缀 Stream 保持原路由。
ChangedTFMs: 生产 `netstandard2.0`；验证 `net6.0`/`net8.0`。
RiskLevel: LOW。数组仅在调用方已经拥有同一 `string[]` 时复用，其他 `IList<string>` 保留复制行为。

## 验收

- FreeRedis、CSRedis 生产 `netstandard2.0` 构建通过。
- 两个 Provider 的批量契约精准测试覆盖 List/Stream 顺序、重复、缺失、特殊键和取消。
- 共享缓存测试和 `git diff --check` 通过。
- 更新执行记录、TODO 和生产符号到测试映射；记录 FIX-007 仍未闭环。

## 非目标

- 不重复运行已被 Review 标记为无进展的同类 MGET、字符串 MGET或序列化基准实验。
- 不重建固定 baseline，不放宽性能预算，不处理外部 Cluster/多进程环境门禁。

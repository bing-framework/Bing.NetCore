# BING-CACHE-TODO-20261006-031：List API 批量读取快路径

## 目标

处理缓存性能审查中一个新的分配根因：FreeRedis/CSRedis 的兼容 `GetAllAsync` 和 `GetByPrefixAsync` 当前通过流式 API 收集 `CacheEntry<T>`，List 调用方并不需要逻辑键和命中标记，却承担了每项 entry、逻辑键列表和流式状态机开销。本轮为 List API 恢复直接批量反序列化快路径，保持 Stream API 的逐项命中语义不变。

## 实施范围

1. FreeRedis：为 `GetAllAsync` 和 `GetByPrefixAsync` 增加批量值读取内部实现；单节点继续使用有限 MGET，Cluster/分片沿用现有路由和预算校验。
2. CSRedis：为 `GetAllAsync` 和 `GetByPrefixAsync` 增加批量值读取内部实现；保留现有 Cluster/分片分组与取消语义。
3. 不改变同步 API、流式 API、命名空间过滤、物理键格式、租约、批大小或资源预算。
4. 补充 List/Stream 等值一致性、缺失/default、重复键、取消和空输入回归测试。
5. 使用固定 baseline 复用规则，仅运行受影响 Provider 精准测试；若需要性能探针，使用同一端点和既有 1k 方向性数据，不重复已否定的实验。

## 约束与影响分析

- ChangedFiles：两个 Redis Provider 的 `GetAllAsync`/`GetByPrefixAsync` 及其私有批量读取辅助；对应 Provider 测试和任务文档。
- ChangedProjects：`Bing.Caching.FreeRedis`、`Bing.Caching.CSRedis` 及其集成测试。
- ChangedPublicContracts：无。
- ChangedRuntimePaths：List 异步批量读取、前缀扫描回读。
- ChangedProviders：FreeRedis、CSRedis；EasyCaching 不改。
- ChangedTFMs：net6.0、net8.0 测试；生产 netstandard2.0 构建。
- RiskLevel：MEDIUM。主要风险是 List 结果顺序、缺失值默认值、集群/分片路由和取消语义变化。

## 验收

- `GetAllAsync` 保持输入顺序、重复键位置和缺失/default 语义；空集合不发送 Redis 命令。
- `GetByPrefixAsync` 保持扫描结果值集合和命名空间隔离；取消在下一批派发前生效。
- Stream 与 List 对同一输入的值结果一致。
- 受影响精准测试 net6/net8 通过；两个 Provider 的 netstandard2.0 生产构建通过；`git diff --check` 通过。
- 性能预算若未达，保留为独立 `OPEN_ACTIONABLE`/`BLOCKED_EXTERNAL` 证据，不宣称关闭 FIX-007。

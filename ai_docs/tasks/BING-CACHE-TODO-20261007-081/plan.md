# BING-CACHE-TODO-20261007-081：EasyCaching 原生 SCAN 页预算

## 目标

关闭 EasyCaching Redis/Hybrid 扫描路径对 `IServer.Keys/KeysAsync` 隐藏分页的依赖，使 `CacheResourceOptions.MaxScanPages` 能在真实 StackExchange.Redis `SCAN` 页边界上生效。保留自定义 EasyCaching Provider 的键流边界，并明确其仍只受 `MaxScanKeys` 保护。

## Change Impact Analysis

- ChangedFiles：`Bing.EasyCaching` 扫描实现、EasyCaching 精准/集成测试、任务文档和权威 TODO 索引。
- ChangedProjects：`Bing.EasyCaching`、`Bing.EasyCaching.Tests`；必要时新增真实 Redis 门禁测试。
- ChangedPublicContracts：无；复用既有 `CacheResourceOptions.MaxScanPages` 和 `CacheScanLimitExceededException`。
- ChangedRuntimePaths：EasyCaching Redis/Hybrid 单节点、Cluster 主节点和分片内部管理器的同步/异步前缀扫描、模式清理和 Clear。
- ChangedProviders：StackExchange.Redis `IDatabase` 路径；非 Redis/custom Provider 的 `GetAllKeysByPrefix*` 路径保持既有行为。
- ChangedTFMs：生产 `netstandard2.0`；测试 `net6.0`、`net8.0`。
- RiskLevel：MEDIUM。原生 `SCAN` 响应解析、Cluster 主节点遍历和取消/页预算计数需要保持与现有拓扑校验一致。

## 实施步骤

### P1：统一显式 SCAN 内部迭代器

- 增加同步和异步的 StackExchange.Redis 原生 `SCAN` 迭代器，使用当前命名空间/逻辑模式、批大小和主节点服务器。
- 发出每一页前调用 `CacheScanBudget.ConsumePage()`；每页通过 `CacheCommandProbe.RecordScanPage` 记录页与返回项。
- 同步、异步、前缀 List、前缀 Stream、模式删除、前缀删除和 Clear 共用该迭代器。
- 异步迭代器在派发下一页前检查取消；StackExchange.Redis 原始执行不支持令牌撤销，已发送命令不承诺撤销。

### P2：配置预算和分片接入

- EasyCaching 内部默认创建预算时传入完整 `CacheResourceOptions`，使 `MaxScanPages` 不被丢弃。
- `ShardedRedisCacheManager` 的共享预算也传入完整资源配置，确保多分片扫描的页数按一次操作累计。
- 保留 Cluster 拓扑完整性校验、命名空间过滤、失败显式传播和自定义 Provider 的现有边界。

### P3：验证与证据

- 新增单元测试验证原生 SCAN 响应解析、页计数、空页和取消。
- 使用隔离 Redis 7.4 实例验证 EasyCaching Redis 和 Hybrid 的同步/异步/流式前缀扫描、删除和 Clear 在 `MaxScanPages=1` 时只发送一页匹配命名空间的 `SCAN`。
- 验证相似命名空间和特殊逻辑键不会扩大匹配范围；完成后清理临时 Redis。
- 运行 EasyCaching 精准测试、受影响 TFM、生产 `netstandard2.0` 构建和 `git diff --check`。
- 更新任务 `TODO.md`、`execution.md`、生产符号到测试方法映射及权威索引：只关闭 EasyCaching Redis/Hybrid 原生 SCAN 页预算；真实网络往返、客户端内部排队、自定义 Provider 隐藏分页仍保留为延后项。

## 非目标

- 不新增公共流式 API，不改变 Provider 注册方式、物理键格式、Hybrid 最终一致语义或 Redis 租约语义。
- 不把自定义 EasyCaching Provider 的未知分页实现伪装为可观测的 `SCAN` 页。
- 不触碰正式性能预算、100,000 项矩阵、双进程回源和独立总线故障证据。

## Definition of Done

- 所有 IDatabase 扫描入口都使用显式原生 `SCAN`，并在下一页前执行 `MaxScanPages` 闸门。
- Redis/Hybrid 单节点真实门禁覆盖同步、异步、流式读取、删除和 Clear；页数、异常类型和命名空间匹配命令数有断言。
- net6/net8 测试和 netstandard2.0 生产构建通过；任务证据和权威索引状态一致。

# BING-CACHE-TODO-20261007-081 生产符号到测试方法映射

| 生产符号/路径 | 验证行为 | 测试方法 |
| --- | --- | --- |
| `CacheManager.EnumerateRedisKeys` | 同步原生 SCAN 页预算、命名空间模式和响应解析 | `ScanPageBudgetIntegrationTest.RedisAndHybrid_RespectNativeScanPageBudget`（同步 GetByPrefix/RemoveByPrefix/Clear） |
| `CacheManager.EnumerateRedisKeysAsync` | 异步原生 SCAN 页预算、取消和响应解析 | `ScanPageBudgetIntegrationTest.RedisAndHybrid_RespectNativeScanPageBudget`（异步 GetByPrefix/RemoveByPrefix/Clear） |
| `CacheManager.EnumeratePhysicalPrefixKeysAsync`、`GetByPrefixStreamCoreAsync` | EasyCaching Redis/Hybrid 前缀 List/Stream 扫描 | `ScanPageBudgetIntegrationTest.RedisAndHybrid_RespectNativeScanPageBudget`（流式前缀读取）；`RedisCoordinatorIntegrationTest.RedisScan_HandlesSpecialKeysWithoutTouchingAnotherNamespace`；`HybridScan_HandlesSpecialKeysWithoutTouchingAnotherNamespace` |
| `CacheManager.EnumerateNamespaceKeys`、`VisitKeysAsync` | 同步/异步前缀、模式删除和 Clear | `ScanPageBudgetIntegrationTest.RedisAndHybrid_RespectNativeScanPageBudget`；`RedisCoordinatorIntegrationTest` 21 项 net6/net8 回归 |
| `ShardedRedisCacheManager` 共享扫描预算 | 分片管理器不丢弃 `MaxScanPages` | `Bing.EasyCaching.Tests` 全量 net6/net8 回归；真实多节点分片门禁仍由外部环境控制 |

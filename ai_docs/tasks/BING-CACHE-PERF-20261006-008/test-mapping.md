# 本轮生产符号到测试方法映射

| 生产符号/行为 | 测试方法 | 证据边界 |
| --- | --- | --- |
| `CacheKeyFormat.GetPhysicalKey/GetPhysicalKeys` | `CacheConfigurationAndKeyFormatTest.PhysicalKey_Isolated_AndRoundTripsSpecialCharacters` 及固定物理键测试 | 证明字节格式、Unicode、保留字符和命名空间隔离不变 |
| `CacheKeyFormat.TryGetLogicalKey` | `CachePatternTest`、各 Provider 特殊键/前缀扫描测试 | 证明 tag 校验和逻辑键还原；SCAN 仍非快照 |
| FreeRedis/CSRedis/EasyCaching 扫描循环 | 各 Provider `BatchContractTest`、`NamespaceIsolationTest`、EasyCaching `RedisCoordinatorIntegrationTest` | 真实 Redis 门控；不替代 Cluster 故障环境 |
| 批量性能热路径 | CacheBench `batchRead`、`scan`、流式阶段 | 当前候选绝对指标；不自动关闭正式预算 |

# BING-CACHE-TODO-20261007-083 生产符号到测试方法映射

| 生产符号/路径 | 验证行为 | 测试方法 |
| --- | --- | --- |
| `CacheManager.EnumerateRedisKeysAsync`、`GetByPrefixStreamCoreAsync` | SCAN 已执行但响应丢失时传播底层异常，不返回完整成功 | `EasyScanInFlightFailureTest.RedisScan_ResponseLostAfterExecution_PropagatesFailure` |
| `CacheManager.Cluster.ValidateClusterScanTopologyAsync`、`EnsureClusterScanTopologyUnchangedAsync` | 流式扫描期间槽位归属变化被拒绝 | `EasyClusterIntegrationTest.RedisCluster_SlotOwnershipChangeDuringPrefixStreamIsRejected` |

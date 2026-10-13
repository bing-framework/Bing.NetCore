# BING-CACHE-TODO-20261007-086 生产符号到测试方法映射

| 生产符号 | 行为 | 测试方法 | 状态 |
| --- | --- | --- | --- |
| `CacheManager.EnumerateRedisKeysAsync` / Cluster 扫描 | 代理暂存完整 SCAN 响应时流式操作保持在途；连接中断后传播异常 | `EasyScanInFlightFailureTest.RedisClusterScan_ResponseLostAfterExecution_PropagatesFailure` | 通过 |
| `ValidateClusterScanTopologyAsync` / Cluster 公告 | 三主节点临时公告代理端口，恢复后还原原始端口和总线公告 | 同上 | 通过 |
| `RedisScanResponseGate` | 默认回环监听保持兼容，Cluster 场景可显式监听主机可达地址 | `RedisScanResponseGate` 构造函数回归 + 上述门禁 | 通过 |

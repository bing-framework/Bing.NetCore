# BING-CACHE-TODO-20261007-092 生产符号到测试方法映射

| 生产符号 | 行为 | 测试方法 | 状态 |
| --- | --- | --- | --- |
| `CacheManager.GetByPrefixStreamAsync` | SCAN 响应挂起期间主节点重启不得返回完整成功 | `EasyClusterScanRestartIntegrationTest.RedisClusterScan_MasterRestartDuringHeldResponse_PropagatesFailure` | 通过：net6.0/net8.0，各 1/1 |
| EasyCaching Redis Cluster Provider | 故障后重建 Manager 恢复完整拓扑和前缀读取 | `EasyClusterScanRestartIntegrationTest.RedisClusterScan_MasterRestartDuringHeldResponse_PropagatesFailure` | 通过：net6.0/net8.0，各 1/1 |
| `CacheManager.GetAllAsync` / `ClearAsync` | 恢复后跨槽批量读取和命名空间清理保持隔离 | `EasyClusterScanRestartIntegrationTest.RedisClusterScan_MasterRestartDuringHeldResponse_PropagatesFailure` | 通过：net6.0/net8.0，各 1/1 |

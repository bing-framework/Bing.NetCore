# BING-CACHE-TODO-20261007-090 生产符号到测试方法映射

| 生产符号 | 行为 | 测试方法 | 状态 |
| --- | --- | --- | --- |
| EasyCaching Redis Cluster Provider | 副本提升后重新发现主节点和完整槽位 | `EasyClusterFailureIntegrationTest.RedisCluster_Failover_RebuiltManagerReadsAndClearsAllSlots` | 通过：net6.0/net8.0，各 1/1 |
| `CacheManager.GetAllAsync` / `GetByPrefixAsync` | 故障转移后跨槽批量读取与前缀扫描保持完整 | `EasyClusterFailureIntegrationTest.RedisCluster_Failover_RebuiltManagerReadsAndClearsAllSlots` | 通过：net6.0/net8.0，各 1/1 |
| `CacheManager.GetByPrefixStreamAsync` | 故障转移后流式前缀读取保持完整 | `EasyClusterFailureIntegrationTest.RedisCluster_Failover_RebuiltManagerReadsAndClearsAllSlots` | 通过：net6.0/net8.0，各 1/1 |
| `CacheManager.ClearAsync` | 故障转移后只清理当前命名空间 | `EasyClusterFailureIntegrationTest.RedisCluster_Failover_RebuiltManagerReadsAndClearsAllSlots` | 通过：net6.0/net8.0，各 1/1 |

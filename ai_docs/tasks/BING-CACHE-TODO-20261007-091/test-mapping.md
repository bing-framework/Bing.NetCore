# BING-CACHE-TODO-20261007-091 生产符号到测试方法映射

| 生产符号 | 行为 | 测试方法 | 状态 |
| --- | --- | --- | --- |
| EasyCaching Redis Cluster Provider | 主节点容器重启后拓扑和槽位恢复 | `EasyClusterRestartIntegrationTest.RedisCluster_MasterRestart_RebuiltManagerRecoversAllSlots` | 通过：net6.0/net8.0，各 1/1 |
| `CacheManager.GetAllAsync` / `GetByPrefixAsync` | 重启后重建 Manager 完成跨槽批量读取和前缀扫描 | `EasyClusterRestartIntegrationTest.RedisCluster_MasterRestart_RebuiltManagerRecoversAllSlots` | 通过：net6.0/net8.0，各 1/1 |
| `CacheManager.GetByPrefixStreamAsync` / `ClearAsync` | 重启后流式读取完整且只清理当前命名空间 | `EasyClusterRestartIntegrationTest.RedisCluster_MasterRestart_RebuiltManagerRecoversAllSlots` | 通过：net6.0/net8.0，各 1/1 |

# BING-CACHE-TODO-20261007-085 生产符号到测试方法映射

| 生产符号 | 行为 | 测试方法 | 状态 |
| --- | --- | --- | --- |
| `ShardedRedisCacheManager` / Hybrid ClientSharded | 两个数据分片同时重启时识别节点身份变化并拒绝旧管理器继续返回伪成功 | `EasyShardedHybridTwoNodeBusFaultIntegrationTest.ShardedHybrid_BusPauseOverlapsBothShardRestarts_RejectsPendingUpdateAndRecovers` | 待运行 |
| `HybridBusFailureTracker` / Hybrid 发布 | 独立总线暂停期间分布式提交后的发布失败传播，恢复后重新建立订阅和跨实例失效 | 同上 | 待运行 |
| `RedisLeaseCoordinator` / Hybrid 重建 | 数据分片和总线恢复后，新管理器重新提交并使对端本地层失效 | 同上 | 待运行 |

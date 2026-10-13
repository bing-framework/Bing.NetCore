# 生产符号到测试方法

| 生产符号/行为 | 测试项目 | 测试方法 |
| --- | --- | --- |
| `FreeRedisCacheManager.GetAllStreamAsync` 客户端分片顺序、重复、缺失、null、Hit | `Bing.Caching.FreeRedis.Tests.Integration` | `ShardedCacheTest.TwoShards_BatchStreamAndPrefixStreamRemainConsistent` |
| `FreeRedisCacheManager.GetByPrefixStreamAsync` 客户端分片扫描和命名空间隔离 | `Bing.Caching.FreeRedis.Tests.Integration` | `ShardedCacheTest.TwoShards_BatchStreamAndPrefixStreamRemainConsistent` |
| `CSRedisCacheManager.GetAllStreamAsync` 客户端分片跨节点顺序与 Hit | `Bing.Caching.CSRedis.Tests.Integration` | `ShardedCacheTest.TwoShards_BatchSetAndGetAcrossBothNodes` |
| `CSRedisCacheManager.GetByPrefixStreamAsync` 客户端分片扫描和命名空间隔离 | `Bing.Caching.CSRedis.Tests.Integration` | `ShardedCacheTest.TwoShards_PrefixListAndStreamRemainConsistentAcrossBothNodes` |

## 验证边界

以上测试继续受 `ShardedFact` 和 `BING_TEST_REDIS_SHARD_CONNECTION` 门控。门控关闭时只能证明测试项目编译成功，不能证明双节点运行通过。

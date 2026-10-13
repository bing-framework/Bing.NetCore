# 生产符号到测试方法映射

| 生产符号 | 关键行为 | 测试项目与方法 |
| --- | --- | --- |
| `FreeRedisCacheManager.SetAllAsync<T>` | 客户端分片按路由客户端分组；同客户端顺序写入；跨客户端有界并发；覆盖写入 | `Bing.Caching.FreeRedis.Tests.Integration` / `ShardedCacheTest.TwoShards_BatchSetAndGetAcrossBothNodes`、`ShardedCacheTest.TwoShards_BatchStreamAndPrefixStreamRemainConsistent` |
| `FreeRedisCacheManager.ReadPhysicalBatch<T>` | 单键批次使用 GET，多键批次使用 MGET，保持缺失/default 和顺序 | `Bing.Caching.FreeRedis.Tests.Integration` / `ShardedCacheTest.TwoShards_BatchSetAndGetAcrossBothNodes`、`ShardedCacheTest.TwoShards_BatchStreamAndPrefixStreamRemainConsistent` |
| `FreeRedisCacheManager.ReadRawBatchAsync` | 异步单键/多键读取、取消检查和原始响应长度校验 | `Bing.Caching.FreeRedis.Tests.Integration` / `ShardedCacheTest.TwoShards_BatchStreamAndPrefixStreamRemainConsistent`、`ShardedCacheTest.OneShardUnavailable_ScanBackedReadsAndClearThrowInsteadOfReturningPartialSuccess` |
| `FreeRedisCacheManager.ClientForPhysicalKey` | 物理键跨两个客户端路由、两节点扫描和 Clear | `Bing.Caching.FreeRedis.Tests.Integration` / `ShardedCacheTest.TwoShards_ScanAndClearAcrossBothNodes`、`ShardedCacheTest.RuntimeTypeRead_UsesSerializerFromRoutedShard` |
| `CSRedisCacheManager` 客户端分片路径 | CSRedis 双节点批量、流式、扫描、路由和故障边界回归 | `Bing.Caching.CSRedis.Tests.Integration` / `ShardedCacheTest` 全部 7 个测试方法 |

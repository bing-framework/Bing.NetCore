# 生产符号到测试方法

| 生产符号 | 测试方法 |
| --- | --- |
| `FreeRedisCacheManager.GetByPrefixStreamAsync` | `Bing.Caching.FreeRedis.Tests.CommandCancellationTest.GetByPrefixStream_CancellationDoesNotDispatchSecondMGet` |
| `CSRedisCacheManager.GetByPrefixStreamAsync` | `Bing.Caching.CSRedis.Tests.CommandCancellationTest.GetByPrefixStream_CancellationDoesNotDispatchSecondMGet` |
| `CacheManager.GetByPrefixStreamAsync` | `Bing.EasyCaching.Tests.CacheManagerTests.GetByPrefixStream_UsesBatchesAndReturnsLogicalKeys`、`GetAllStream_CacheKeys_CancellationAfterFirstBatch_DoesNotDispatchNextBatch` |
